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
        var step = context.CurrentStep!.Step;
        var serializer = new VisaWaitOpcPlugin().CreateSerializer();
        var setting = (VisaWaitOpcSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
            var key = VisaHelper.GetSessionKey(connName);

            if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not IMessageBasedSession session)
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

            // 如果指定了超时，临时修改会话超时
            var originalTimeout = session.TimeoutMilliseconds;
            if (setting.TimeoutMs > 0)
                session.TimeoutMilliseconds = setting.TimeoutMs;

            try
            {
                var response = VisaHelper.Query(session, "*OPC?", true);
                context.LogAction?.Invoke($"VISA WaitOPC: {connName} 操作完成 (响应: {response})");

                return new ExecutionResult
                {
                    StepResult = new StepResult { Status = TestStatus.Passed, Value = "操作完成" }
                };
            }
            finally
            {
                // 恢复原始超时
                if (setting.TimeoutMs > 0)
                    session.TimeoutMilliseconds = originalTimeout;
            }
        }
        catch (OperationCanceledException)
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
                    Error = new ErrorInfo { Message = $"VISA WaitOPC 超时或失败: {ex.Message}" }
                }
            };
        }
    }
}
