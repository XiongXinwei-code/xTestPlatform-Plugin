using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402.Executors;

public abstract class ExecutorBase<T> : IStepExecutor where T : AxisSetting, new()
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        T? setting = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.IsAbortRequested) return ExecutionResults.Aborted();
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("当前步骤不存在");
            var serializer = StepCatalog.ForSetting(typeof(T)).CreateSerializer();
            setting = step.StepSetting.Setting is { Length: > 0 } data
                ? (T)serializer.Deserialize(data, step.StepSetting.SettingVersion) : (T)serializer.CreateDefault();
            var validation = SettingValidation.Validate(setting);
            if (validation.Count != 0) throw new ArgumentException(string.Join("；", validation));
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(setting.TimeoutMs);
            var values = await SettingValidation.EvaluateAsync(setting, context, deadline.Token).ConfigureAwait(false);
            await RunAsync(setting, values, context, deadline.Token, cancellationToken).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (context.IsAbortRequested) return ExecutionResults.Aborted();
            context.Log("CiA402 步骤执行完成");
            return ExecutionResults.Completed();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            return ExecutionResults.Aborted();
        }
        catch (OperationCanceledException)
        {
            return ExecutionResults.Error($"CiA402 步骤超过总超时（{setting?.TimeoutMs ?? 0} ms）");
        }
        catch (TimeoutException ex)
        {
            return ExecutionResults.Error($"CiA402 通信或等待超时（步骤上限 {setting?.TimeoutMs ?? 0} ms）：{ex.Message}");
        }
        catch (Exception ex)
        {
            context.Log(LogLevel.Error, "CiA402 步骤执行错误", ex.ToString());
            return ExecutionResults.Error($"CiA402 执行失败：{ex.Message}");
        }
    }

    protected abstract Task RunAsync(T setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken runToken);
}

public abstract class AxisActionExecutor<T> : ExecutorBase<T> where T : AxisSetting, new()
{
    protected virtual bool IsControl => true;
    protected override Task RunAsync(T setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken runToken)
    {
        var name = values.Text(nameof(AxisSetting.AxisName));
        if (!context.Resources.TryGet<AxisSession>(AxisSession.Key(name), out var session))
            throw new InvalidOperationException($"轴会话 {name} 不存在，请先执行 CiA402_BindAxis");
        return session.ExecuteAsync((axis, token) => OperateAsync(setting, values, session, context, token), IsControl, setting.PollIntervalMs, ct);
    }
    protected abstract Task OperateAsync(T setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct);
}
