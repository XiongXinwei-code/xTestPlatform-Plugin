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
        var step = context.CurrentStep!.Step;
        var serializer = new VisaReadPlugin().CreateSerializer();
        var setting = (VisaReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
            var varName = await Evaluator.EvaluateAsync<string>(setting.ResultVariable, context) ?? setting.ResultVariable;
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

            var response = VisaHelper.Read(session, setting.TrimResponse);
            context.SetVariable(varName, response);

            context.LogAction?.Invoke($"VISA Read: {response}");
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = response }
            };
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
                    Error = new ErrorInfo { Message = $"VISA 读取失败: {ex.Message}" }
                }
            };
        }
    }
}
