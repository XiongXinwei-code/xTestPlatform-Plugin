using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 写入执行器，向仪器发送 SCPI 命令
/// </summary>
public sealed class VisaWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>执行 VISA 写入操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new VisaWritePlugin().CreateSerializer();
        var setting = (VisaWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var command = await Evaluator.EvalStringAsync(setting.Command, context);
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

            VisaHelper.Write(session, command, GetTerminator(context, connName));

            context.LogAction?.Invoke($"VISA Write: {command}");
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = command }
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
                    Error = new ErrorInfo { Message = $"VISA 写入失败: {ex.Message}" }
                }
            };
        }
    }

    /// <summary>获取打开会话时保存的终止符，未找到时默认换行符</summary>
    private static string GetTerminator(IExecutionContext context, string connName) =>
        context.CurrentStep!.RuntimeData.TryGetValue(VisaHelper.GetTerminatorKey(connName), out var t) && t is string term ? term : "\n";
}
