using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqAiReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<DaqTaskResource, int, int, bool, DaqReadResult> _read;
    public NiDaqAiReadExecutor() => _read = DaqReadCore.Read;
    internal NiDaqAiReadExecutor(Func<DaqTaskResource, int, int, bool, DaqReadResult> read) => _read = read;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new NiDaqAiReadPlugin().CreateSerializer();
            var setting = (NiDaqAiReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            bool save = DaqExportPolicy.ValidateReadRequest(setting.ExportFormat, setting.SaveToFile, setting.ResultVariable, setting.SamplesToRead, setting.ReadTimeoutMs, setting.MaxFileSizeMB);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.ExportTimeoutMs);
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            guard.Check();
            var resultVar = setting.ResultVariable;

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            var resource = NiDaqTaskRegistry.Get(context, taskName);
            guard.EnsureCurrent(taskName, resource);
            var acquired = await resource.RunAsync(() => { guard.EnsureCurrent(taskName, resource); return _read(resource, setting.SamplesToRead, setting.ReadTimeoutMs, false); }, setting.ReadTimeoutMs, "AI 读取", cancellationToken);
            guard.EnsureCurrent(taskName, resource);
            int channels = acquired.Data.GetLength(0), aiChannels = channels, samples = acquired.Data.GetLength(1);
            var outputDir = save && !string.IsNullOrWhiteSpace(setting.OutputDirectory)
                ? await Evaluator.EvalStringAsync(setting.OutputDirectory, context) : "";
            var path = save ? DaqFileWriter.BuildFilePath(outputDir, taskName + "_AI", "csv") : null;
            guard.EnsureCurrent(taskName, resource);
            var waveform = await resource.RunAsync(() =>
            {
                guard.EnsureCurrent(taskName, resource);
                if (save) DaqFileWriter.AppendCsv(path!, acquired.Data, acquired.Names, setting.MaxFileSizeMB,
                    m => context.Log(m), resource.EnsureOperationActive);
                return string.IsNullOrWhiteSpace(resultVar) ? null : acquired.ToWaveform(taskName);
            }, setting.ExportTimeoutMs, "AI CSV 导出及结果构造", cancellationToken);
            guard.Check();
            guard.EnsureCurrent(taskName, resource);
            if (waveform != null) DaqResultPublisher.Publish(context, resultVar, waveform, cancellationToken, resource, taskName);
            context.Log("DAQ 数据按采样索引对齐；硬件采集起点未建立 UTC 关联，Waveform.StartTime=DateTime.MinValue（未知），不使用读取完成时间伪造采集时间");
            guard.EnsureCurrent(taskName, resource);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"{channels} ch × {samples} samples"
                }
            };
        }
        catch (TimeoutException ex)
        {
            return ErrorResult(ex.Message, ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return ErrorResult($"AI 读取失败：{ex.Message}", ex);
        }
    }

    private static ExecutionResult ErrorResult(string message, Exception? ex = null) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = ex is null ? new ErrorInfo { Message = message } : ErrorInfo.FromException(ex, message)
        }
    };
}
