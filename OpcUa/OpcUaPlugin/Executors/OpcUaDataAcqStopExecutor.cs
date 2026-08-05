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

            // 获取节点配置
            var items = context.CurrentStep.RuntimeData.TryGetValue(taskKey + "_items", out var itemsObj)
                ? (List<OpcUaDataAcqItem>)itemsObj
                : new List<OpcUaDataAcqItem>();

            // 停止采集
            var records = await acqTask.StopAsync();
            acqTask.Dispose();
            context.CurrentStep.RuntimeData.Remove(taskKey);
            context.CurrentStep.RuntimeData.Remove(taskKey + "_items");

            // 导出 CSV
            if (setting.ExportFormat is DataAcqExportFormat.Csv or DataAcqExportFormat.Both)
            {
                var csvPath = await Evaluator.EvalStringAsync(setting.CsvFilePath, context);
                OpcUaDataAcqTask.ExportToCsv(csvPath, items, records);
                context.LogAction?.Invoke($"数据已导出到: {csvPath} ({records.Count} 条记录)");
            }

            // 保存统计值到变量
            if (setting.SaveStatistics && (setting.ExportFormat is DataAcqExportFormat.Variable or DataAcqExportFormat.Both))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var colName = string.IsNullOrWhiteSpace(items[i].ColumnName) ? $"Col{i}" : items[i].ColumnName;
                    var stats = OpcUaDataAcqTask.CalculateStatistics(i, records);
                    context.SetVariable($"{setting.StatVariablePrefix}{colName}_Avg", stats.Average);
                    context.SetVariable($"{setting.StatVariablePrefix}{colName}_Max", stats.Max);
                    context.SetVariable($"{setting.StatVariablePrefix}{colName}_Min", stats.Min);
                    context.SetVariable($"{setting.StatVariablePrefix}{colName}_Count", stats.Count);
                }
            }

            context.LogAction?.Invoke($"OPC UA 数据采集已停止: {taskName} (共 {records.Count} 条记录)");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"采集完成: {records.Count} 条记录" }
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
