using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 数据采集停止执行器</summary>
public sealed class OpcUaDataAcqStopExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaDataAcqStopPlugin().CreateSerializer();
        var setting = (OpcUaDataAcqStopSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var taskKey = $"OpcUaDataAcq_{taskName}";

            // 获取采集任务
            if (!context.CurrentStep.RuntimeData.TryGetValue(taskKey, out var obj) || obj is not OpcUaDataAcqTask acqTask)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"采集任务 {taskName} 不存在或未启动" }
                    }
                };
            }

            // 停止采集并释放资源（未消费的缓冲数据丢弃）
            var records = await acqTask.StopAsync();
            acqTask.Dispose();
            context.CurrentStep.RuntimeData.Remove(taskKey);
            context.CurrentStep.RuntimeData.Remove(taskKey + "_items");

            context.LogAction?.Invoke($"OPC UA 数据采集已停止: {taskName} (丢弃未消费数据 {records.Count} 条)");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"采集已停止: {taskName}" }
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
                    Error = new ErrorInfo { Message = $"停止数据采集失败: {ex.Message}" }
                }
            };
        }
    }
}
