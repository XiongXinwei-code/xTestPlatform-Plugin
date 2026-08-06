using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinCyclicSendStopExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new LinCyclicSendStopPlugin().CreateSerializer();
        var setting = (LinCyclicSendStopSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var taskKey  = LinCyclicSendStartExecutor.GetTaskKey(taskName);

            if (context.CurrentStep.RuntimeData.TryGetValue(taskKey, out var obj) && obj is CancellationTokenSource cts)
            {
                await cts.CancelAsync();
                cts.Dispose();
                context.CurrentStep.RuntimeData.Remove(taskKey);
                context.LogAction?.Invoke($"LIN 周期发送已停止: TaskName={taskName}");

                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Passed,
                        Value  = $"已停止周期发送任务: {taskName}"
                    }
                };
            }

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value  = $"未找到运行中的周期发送任务: {taskName}"
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
                    Error  = new ErrorInfo { Message = $"LIN 周期发送停止失败: {ex.Message}" }
                }
            };
        }
    }
}
