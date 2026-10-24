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
/// VISA 读取执行器，从仪器读取响应（不发送命令）
/// </summary>
public sealed class VisaReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>执行 VISA 读取操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new VisaReadPlugin().CreateSerializer();
            var setting = (VisaReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            epoch.ThrowIfExpired();
            var varName = setting.ResultVariable;
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

            epoch.ThrowIfExpired();
            var response = await resource.RunBlockingAsync(
                session => { epoch.ThrowIfExpired(); return VisaHelper.Read(session, setting.TrimResponse); },
                resource.IoTimeoutMs, "VISA 读取", cancellationToken);
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureActive(context, key, resource, cancellationToken);
            context.SetVariable(varName, response);

            context.Log($"VISA Read: {response}");
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureActive(context, key, resource, cancellationToken);
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = response }
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
                    Error = ErrorInfo.FromException(ex, $"VISA 读取超时: {ex.Message}")
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
                    Error = ErrorInfo.FromException(ex, $"VISA 读取失败: {ex.Message}")
                }
            };
        }
    }
}
