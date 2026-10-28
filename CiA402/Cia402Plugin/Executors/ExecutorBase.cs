using CiA402.Core;
using CiA402.Models;
using System.Diagnostics;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402.Executors;

public abstract class ExecutorBase<T> : IStepExecutor where T : AxisSetting, new()
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        T? setting = null;
        EvaluatedSettings? values = null;
        var clock = Stopwatch.StartNew();
        var invocation = "尚未读取当前步骤";
        var stage = "设置解析与校验";
        CancellationToken stepToken = default;
        Action? rollback = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = PluginCommon.ExecutionEpoch.Capture(context.Resources, "CiA402");
            void CheckExecution()
            {
                cancellationToken.ThrowIfCancellationRequested();
                epoch.ThrowIfExpired();
                if (setting != null && clock.ElapsedMilliseconds >= setting.TimeoutMs)
                    throw new TimeoutException($"CiA402 步骤超过配置总期限 {setting.TimeoutMs} ms，禁止迟到提交或发布成功");
            }
            invocation = AxisFailureContext.Step(context);
            CheckExecution();
            if (context.IsAbortRequested) return ExecutionResults.Aborted();
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("当前步骤不存在");
            var serializer = AxisSettingSerializers.For(new T());
            setting = step.StepSetting.Setting is { Length: > 0 } data
                ? (T)serializer.Deserialize(data, step.StepSetting.SettingVersion) : (T)serializer.CreateDefault();
            var validation = SettingValidation.Validate(setting);
            if (validation.Count != 0) throw new ArgumentException(string.Join("；", validation));
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stepToken = deadline.Token;
            deadline.CancelAfter(setting.TimeoutMs);
            stage = "表达式与输出变量校验";
            values = await SettingValidation.EvaluateAsync(setting, context, deadline.Token).ConfigureAwait(false);
            values.CheckExecution = CheckExecution;
            CheckExecution();
            stage = "轴步骤执行";
            rollback = await RunAsync(setting, values, context, deadline.Token, cancellationToken, CheckExecution).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (context.IsAbortRequested) throw new OperationCanceledException("步骤完成前执行环境发出中止", cancellationToken);
            context.Log("CiA402 步骤执行完成");
            CheckExecution();
            return ExecutionResults.Completed();
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            // Aborted 没有最终 ErrorInfo，单独保留取消的过程诊断。
            try { context.Log(LogLevel.Debug, "CiA402 步骤响应调用方取消或中止", Detail(ex)); } catch (Exception) { }
            return ExecutionResults.Aborted();
        }
        catch (OperationCanceledException ex)
        {
            var message = stepToken.IsCancellationRequested
                ? $"CiA402 步骤超过总超时（{setting?.TimeoutMs ?? 0} ms）"
                : "CiA402 轴会话或通信后端取消了本次操作，步骤总期限尚未到达";
            return ExecutionResults.Error(message, Detail(ex));
        }
        catch (TimeoutException ex)
        {
            return ExecutionResults.Error($"CiA402 通信或等待超时（步骤上限 {setting?.TimeoutMs ?? 0} ms）：{ex.Message}", Detail(ex));
        }
        catch (Exception ex)
        {
            // SDK 1.2.8 由引擎记录最终 Error，避免这里重复输出同一错误。
            return ExecutionResults.Error($"CiA402 执行失败：{ex.Message}", Detail(ex));
        }

        string Detail(Exception error)
        {
            // 仅创建资源的步骤提供回滚；保留本次精确实例，不按名称关闭后来替换的会话。
            var cleanup = rollback;
            rollback = null;
            try { cleanup?.Invoke(); }
            catch (Exception cleanupError) { error = new AggregateException("步骤执行失败，且本次新资源回收失败", error, cleanupError); }
            try
            {
                return AxisFailureContext.Detail(invocation, setting, values, context, stage,
                    clock.ElapsedMilliseconds, cancellationToken.IsCancellationRequested, error)
                    + $"{Environment.NewLine}步骤令牌取消={stepToken.IsCancellationRequested}";
            }
            catch (Exception diagnosticError)
            {
                // 资源表可能正被宿主回收，补充诊断失败不能替换真正的执行异常。
                return $"{invocation}; 耗时={clock.ElapsedMilliseconds} ms; 补充诊断失败={diagnosticError.Message}{Environment.NewLine}{AxisFailureContext.ExceptionDetail(error)}";
            }
        }
    }

    // callerToken 是本步骤调用令牌，不是共享资源的执行生命周期令牌。
    protected abstract Task<Action?> RunAsync(T setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken callerToken, Action checkExecution);
}

public abstract class AxisActionExecutor<T> : ExecutorBase<T> where T : AxisSetting, new()
{
    protected virtual bool IsControl => true;
    protected override async Task<Action?> RunAsync(T setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken callerToken, Action checkExecution)
    {
        var name = values.Text(nameof(AxisSetting.AxisName));
        checkExecution();
        if (!context.Resources.TryGet<AxisSession>(AxisSession.Key(name), out var session))
            throw new InvalidOperationException($"轴会话 {name} 不存在，请先执行 CiA402_BindAxis");
        checkExecution();
        try
        {
            await session.ExecuteAsync(async (axis, token) =>
            {
                void CheckAction() { checkExecution(); session.CheckOperationLifetime(); }
                CheckAction();
                if (!context.Resources.TryGet<AxisSession>(AxisSession.Key(name), out var selected) || !ReferenceEquals(selected, session))
                    throw new InvalidOperationException("轴会话已被移除或替换，禁止旧步骤提交动作");
                checkExecution();
                // 每次底层 I/O 都重查当前步骤总期限；退出动作后恢复作用域，
                // 会话独立清理不得继承已过期的普通动作边界。
                using var boundary = AxisOperationBoundary.Enter(CheckAction);
                await OperateAsync(setting, values, session, context, token).ConfigureAwait(false);
            }, IsControl, setting.PollIntervalMs, ct,
                AxisFailureContext.Operation(context, setting)).ConfigureAwait(false);
            checkExecution();
            if (!context.Resources.TryGet<AxisSession>(AxisSession.Key(name), out var current) || !ReferenceEquals(current, session))
                throw new InvalidOperationException("轴会话已被移除或替换，不能发布旧调用结果");
        }
        catch (Exception ex) { AxisFailureContext.AttachSession(ex, session); throw; }
        return null;
    }
    protected abstract Task OperateAsync(T setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct);
    protected static void Output(IExecutionContext context, EvaluatedSettings values, AxisSession session, CancellationToken ct, string path, object value)
    {
        void CheckPublication()
        {
            ct.ThrowIfCancellationRequested();
            values.CheckExecution?.Invoke();
            session.CheckOperationLifetime();
            var key = AxisSession.Key(values.Text(nameof(AxisSetting.AxisName)));
            if (!context.Resources.TryGet<AxisSession>(key, out var current) || !ReferenceEquals(current, session))
                throw new InvalidOperationException("轴会话已被移除或替换，不能发布旧调用结果");
            values.CheckExecution?.Invoke();
        }
        SettingValidation.Output(context, path, value, CheckPublication);
    }
}
