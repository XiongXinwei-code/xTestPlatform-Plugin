using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 数据采集读取执行器（FIFO 消费）</summary>
public sealed class OpcUaDataAcqReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaDataAcqReadPlugin().CreateSerializer();
        var setting = (OpcUaDataAcqReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var taskKey = $"OpcUaDataAcq_{taskName}";

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            if (!context.Resources.TryGet<OpcUaDataAcqTask>(taskKey, out var acqTask))
                return ErrorResult($"采集任务 {taskName} 不存在或未启动，请先执行 OpcUa_DataAcq_Start");

            // 缓冲溢出报错（仿硬件 FIFO 溢出）
            if (acqTask.HasOverflowed)
                return ErrorResult($"采集任务 {taskName} 缓冲区已溢出（读取不及时），采集已停止，请增大 BufferSize 或提高读取频率");

            // 从 FIFO 缓冲消费数据
            var records = acqTask.Read(setting.SamplesToRead);
            var items = acqTask.Items;

            // 构造波形数据（ResultVariable 为波形类型 Waveform）
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable) && records.Count > 0)
            {
                var sampleRate = acqTask.SamplingIntervalMs > 0 ? 1000.0 / acqTask.SamplingIntervalMs : 0;
                var waveform = new WaveformData
                {
                    TaskID = taskName,
                    SampleRate = sampleRate,
                    StartTime = records[0].Timestamp,
                    Channels = new List<ChannelData>(items.Count)
                };
                for (int i = 0; i < items.Count; i++)
                {
                    var chData = new double[records.Count];
                    for (int s = 0; s < records.Count; s++)
                    {
                        var v = records[s].Values[i];
                        chData[s] = v == null ? double.NaN : Convert.ToDouble(v);
                    }
                    waveform.Channels.Add(new ChannelData
                    {
                        Channel = string.IsNullOrWhiteSpace(items[i].ColumnName) ? items[i].NodeId : items[i].ColumnName,
                        Values = chData
                    });
                }
                context.SetVariable(setting.ResultVariable, waveform);
            }

            // 追加保存 CSV
            if (setting.SaveToFile && records.Count > 0)
            {
                var csvPath = await Evaluator.EvalStringAsync(setting.CsvFilePath, context);
                if (string.IsNullOrWhiteSpace(csvPath))
                    return ErrorResult("CSV 文件路径不能为空");
                OpcUaDataAcqTask.AppendCsv(csvPath, items, records);
                context.LogAction?.Invoke($"数据已追加导出到: {csvPath} ({records.Count} 条记录)");
            }

            context.LogAction?.Invoke($"OPC UA 采集数据读取: {taskName} ({records.Count} 条记录, 缓冲剩余 {acqTask.SampleCount})");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"{items.Count} ch × {records.Count} samples" }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return ErrorResult($"读取采集数据失败: {ex.Message}");
        }
    }

    private static ExecutionResult ErrorResult(string message) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = new ErrorInfo { Message = message }
        }
    };
}
