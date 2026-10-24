using PluginCommon;
using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 等待操作完成执行器，发送 *OPC? 并等待仪器返回
/// </summary>
public sealed class VisaWaitOpcExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>执行 *OPC? 等待操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new VisaWaitOpcPlugin().CreateSerializer();
            var setting = (VisaWaitOpcSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs < 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "等待期限不能为负数，0 表示继承连接 I/O 期限");

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            epoch.ThrowIfExpired();
            var key = VisaHelper.GetSessionKey(connName);

            if (!context.Resources.TryGet<VisaSessionResource>(key, out var resource))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"未找到 VISA 会话: {connName}" }
                    }
                };
            }

            var timeoutMs = setting.TimeoutMs > 0 ? setting.TimeoutMs : resource.IoTimeoutMs;
            epoch.ThrowIfExpired();
            var response = await resource.RunBlockingAsync(session =>
            {
                epoch.ThrowIfExpired();
                return VisaHelper.WithTemporaryTimeout(session, setting.TimeoutMs, () =>
                {
                    var value = VisaHelper.Query(session, "*OPC?", true, resource.Terminator, () => { epoch.ThrowIfExpired(); resource.ThrowIfSubmissionStopped(); });
                    VisaHelper.ValidateOpcResponse(value);
                    return value;
                });
            }, timeoutMs, "VISA WaitOPC", cancellationToken);
            context.Log($"VISA WaitOPC: {connName} 操作完成 (响应: {response})");
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureActive(context, key, resource, cancellationToken);
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = "操作完成" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TimeoutException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"VISA 等待操作完成超时: {ex.Message}")
                }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"VISA WaitOPC 超时或失败: {ex.Message}")
                }
            };
        }
    }

    /// <summary>获取打开会话时保存的终止符，未找到时默认换行符</summary>
    private static string GetTerminator(IExecutionContext context, string connName) =>
        context.Resources.TryGet<string>(VisaHelper.GetTerminatorKey(connName), out var term) ? term : "\n";
}
