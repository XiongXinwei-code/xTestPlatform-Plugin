using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqSyncStopExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqSyncStopPlugin().CreateSerializer();
        var setting = (NiDaqSyncStopSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvaluateAsync<string>(setting.TaskName, context) ?? setting.TaskName;
            var taskKey = $"NiDaqSync_{taskName}";

            if (!context.CurrentStep.RuntimeData.TryGetValue(taskKey, out var obj) || obj is not NiDaqSyncStreamTask syncTask)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"同步采集任务 {taskName} 不存在，请先执行 NiDaq_Sync_Start" }
                    }
                };
            }

            var stats = await syncTask.StopAsync();
            var filePath = context.CurrentStep.RuntimeData[$"{taskKey}_FilePath"] as string ?? "";
            var statPrefix = context.CurrentStep.RuntimeData[$"{taskKey}_StatPrefix"] as string ?? taskName;

            context.SetVariable($"{statPrefix}_FilePath", filePath);

            foreach (var (chName, stat) in stats)
            {
                context.SetVariable($"{statPrefix}_{chName}_Avg", stat.Average);
                context.SetVariable($"{statPrefix}_{chName}_Max", stat.Max);
                context.SetVariable($"{statPrefix}_{chName}_Min", stat.Min);
                context.SetVariable($"{statPrefix}_{chName}_Count", stat.Count);
            }

            syncTask.Dispose();
            context.CurrentStep.RuntimeData.Remove(taskKey);
            context.CurrentStep.RuntimeData.Remove($"{taskKey}_FilePath");
            context.CurrentStep.RuntimeData.Remove($"{taskKey}_StatPrefix");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"同步采集已停止，文件: {filePath}"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"同步采集停止失败：{ex.Message}" }
                }
            };
        }
    }
}
