using OpcUa.Helpers;
using OpcUa.Models;
using Opc.Ua;
using System.Text.Json;
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
        int? configuredTimeoutMs = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new OpcUaExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new OpcUaDataAcqReadPlugin().CreateSerializer();
            var setting = (OpcUaDataAcqReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "消费总期限必须大于 0");
            configuredTimeoutMs = setting.TimeoutMs;
            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            int Remaining()
            {
                var remaining = setting.TimeoutMs - System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                if (remaining <= 0) throw new TimeoutException($"OPC UA 读取/消费/导出总期限 {setting.TimeoutMs} ms 已到");
                return (int)Math.Ceiling(remaining);
            }
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context)
                .WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken);
            guard.Check();
            var taskKey = $"OpcUaDataAcq_{taskName}";

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            if (!context.Resources.TryGet<OpcUaDataAcqTask>(taskKey, out var acqTask))
                return ErrorResult($"采集任务 {taskName} 不存在或未启动，请先执行 OpcUa_DataAcq_Start");

            void CheckPublication()
            {
                guard.Check();
                _ = Remaining();
                if (acqTask.IsStopRequested || context.IsAbortRequested ||
                    !context.Resources.TryGet<OpcUaDataAcqTask>(taskKey, out var current) || !ReferenceEquals(current, acqTask))
                    throw new InvalidOperationException("采集任务已停止或代次已改变，未向序列发布晚到结果");
            }

            string Health() => JsonSerializer.Serialize(new
            {
                acqTask.TaskName, acqTask.IsRunning, acqTask.HasExited, acqTask.HasOverflowed, acqTask.SampleCount,
                acqTask.ConsecutiveFailures, acqTask.LastError, acqTask.LastSuccessUtc, acqTask.LastFailureUtc, acqTask.BackgroundError,
                acqTask.PendingConsumers, acqTask.LastConsumptionError, acqTask.LastExportCommitSequence,
                CancellationError = acqTask.CancellationError?.ToString(),
                WaveformAxis = "record index (sample), not acquisition time"
            });
            void PublishHealth()
            { CheckPublication(); if (!string.IsNullOrWhiteSpace(setting.HealthVariable)) context.SetVariable(setting.HealthVariable, Health()); }
            PublishHealth();
            if (acqTask.HasOverflowed)
                return ErrorResult($"采集任务 {taskName} 缓冲区已溢出（读取不及时），采集已停止，请增大 BufferSize 或提高读取频率");
            if (acqTask.BackgroundError != null) return ErrorResult($"采集任务 {taskName} 后台异常：{acqTask.BackgroundError}");
            var items = acqTask.Items;
            string? csvPath = null;
            if (setting.SaveToFile)
            {
                csvPath = await Evaluator.EvalStringAsync(setting.CsvFilePath, context)
                    .WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken);
                if (string.IsNullOrWhiteSpace(csvPath)) return ErrorResult("CSV 文件路径不能为空，尚未消费缓冲数据");
            }
            var read = await acqTask.ConsumeWithBudgetAsync(setting.SamplesToRead, async (records, operation) =>
            {
                guard.Check();
                operation.ThrowIfSubmissionStopped();
                bool badQuality = records.Any(record => record.StatusCodes.Length != items.Count || record.StatusCodes.Any(code => !StatusCode.IsGood(code)));
                var waveform = !string.IsNullOrWhiteSpace(setting.ResultVariable) && records.Count > 0
                    ? OpcUaDataAcqOutput.CreateWaveform(taskName, items, records) : null;
                var json = !string.IsNullOrWhiteSpace(setting.RecordsJsonVariable) ? JsonSerializer.Serialize(records) : null;
                if (csvPath != null && records.Count > 0)
                {
                    operation.ThrowIfSubmissionStopped();
                    guard.Check();
                    await OpcUaCsvExport.WriteAsync(csvPath, records, operation,
                        path => acqTask.CsvExporter(path, items, records)).ConfigureAwait(false);
                }
                return new PreparedRead(records.Count, badQuality, waveform, json);
            }, Remaining(), cancellationToken, prepared =>
            {
                CheckPublication();
                if (prepared.Waveform != null) context.SetVariable(setting.ResultVariable, prepared.Waveform);
                CheckPublication();
                if (prepared.Json != null) context.SetVariable(setting.RecordsJsonVariable, prepared.Json);
            });
            PublishHealth();
            if (read.BadQuality || acqTask.ConsecutiveFailures > 0)
                return ExecutionResults.Error($"OPC UA 采集 {taskName} 存在非 Good 数据或连续通信失败；连续失败={acqTask.ConsecutiveFailures}",
                    detail: acqTask.LastError ?? "记录质量不是 Good；请查看逐点记录 JSON/CSV", value: $"{read.Count} samples");
            CheckPublication();
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = $"{items.Count} ch × {read.Count} records" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TimeoutException ex) when (configuredTimeoutMs.HasValue)
        {
            return ErrorResult($"读取采集数据超时（配置总期限 {configuredTimeoutMs.Value} ms）：{ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return ErrorResult($"读取采集数据失败: {ex.Message}", ex);
        }
    }

    private sealed record PreparedRead(int Count, bool BadQuality, WaveformData? Waveform, string? Json);

    private static ExecutionResult ErrorResult(string message, Exception? ex = null) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = ex is null ? new ErrorInfo { Message = message } : ErrorInfo.FromException(ex, message)
        }
    };
}
