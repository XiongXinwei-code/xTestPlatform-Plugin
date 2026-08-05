using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 批量写入执行器，按顺序发送多条 SCPI 命令
/// </summary>
public sealed class VisaBatchWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new VisaBatchWritePlugin().CreateSerializer();
        var setting = (VisaBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

            int sent = 0;
            foreach (var item in setting.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var command = await Evaluator.EvaluateAsync<string>(item.Command, context) ?? item.Command;
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                VisaHelper.Write(session, command);
                sent++;
                context.LogAction?.Invoke($"VISA BatchWrite [{sent}]: {command}");

                if (item.DelayMs > 0)
                    await Task.Delay(item.DelayMs, cancellationToken);
            }

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已发送 {sent} 条命令" }
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
                    Error = new ErrorInfo { Message = $"VISA 批量写入失败: {ex.Message}" }
                }
            };
        }
    }
}
