using Opc.Ua.Client;
using System.Diagnostics;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 断开连接执行器</summary>
public sealed class OpcUaDisconnectExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new OpcUaExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new OpcUaDisconnectPlugin().CreateSerializer();
            var setting = (OpcUaDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            guard.Check();
            var key = OpcUaHelper.GetSessionKey(connName);

            var registration = OpcUaHelper.GetRegistrationLock(context.Resources, key);
            guard.Check();
            var startedAt = Stopwatch.GetTimestamp();
            // 先捕获关闭目标，再递增 generation；新 Open 不能被晚到的旧 Close 按名称误杀。
            context.Resources.TryGet<object>(key, out var captured);
            var target = captured as OpcUaConnectionResource;
            guard.Check();
            OpcUaHelper.MarkCloseRequested(context.Resources, key);
            if (captured != null && target == null) throw new InvalidOperationException("OPC UA 资源类型不匹配，不能确认其活动已经退出");
            target?.Dispose();
            if (!await registration.WaitAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken))
            {
                throw new TimeoutException($"OPC UA 等待同名连接建立/关闭事务超过 {setting.TimeoutMs} ms；已请求其收尾，保留原资源，不能报告已断开");
            }
            try
            {
                if (target != null)
                {
                    await target.CloseAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);
                    if (context.Resources.TryGet<OpcUaConnectionResource>(key, out var current) && ReferenceEquals(current, target))
                        context.Resources.Remove(key, dispose: false);
                    context.Log($"OPC UA 关闭请求捕获的原连接及其后台活动已确认退出: {connName}；后续新代次保持不变");
                }
                else context.Log(LogLevel.Warn, $"OPC UA 关闭请求开始时未找到原连接 {connName}；不处理后来注册的新代次");
            }
            finally { registration.Release(); }

            guard.Check();
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = target == null ? $"原连接不存在: {connName}" : $"原连接已断开: {connName}" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"OPC UA 断开失败: {ex.Message}")
                }
            };
        }
    }
}
