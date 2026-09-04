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

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqAiReadPlugin().CreateSerializer();
        var setting = (NiDaqAiReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var resultVar = setting.ResultVariable;

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            var taskObj = NiDaqTaskRegistry.Get(taskName);
            if (taskObj is not DaqTask task)
                return ErrorResult($"未找到任务 '{taskName}'");

            var reader = new AnalogMultiChannelReader(task.Stream);
            task.Stream.Timeout = setting.ReadTimeoutMs > 0 ? setting.ReadTimeoutMs : -1;
            int samplesToRead = setting.SamplesToRead > 0 ? setting.SamplesToRead : task.Stream.AvailableSamplesPerChannel > 0 ? (int)task.Stream.AvailableSamplesPerChannel : 100;
            double[,] data = await NiDaqTimeoutHelper.RunWithTimeoutAsync(
                () => reader.ReadMultiSample(samplesToRead), setting.ReadTimeoutMs, "AI 读取", cancellationToken);

            int channels = data.GetLength(0);
            int samples = data.GetLength(1);

            // 构造波形数据（ResultVariable 为波形类型 Waveform）
            var waveform = new WaveformData
            {
                TaskID = taskName,
                SampleRate = task.Timing.SampleClockRate,
                StartTime = DateTime.Now,
                Channels = new List<ChannelData>(channels)
            };
            for (int ch = 0; ch < channels; ch++)
            {
                var chData = new double[samples];
                for (int s = 0; s < samples; s++)
                    chData[s] = data[ch, s];
                waveform.Channels.Add(new ChannelData
                {
                    Channel = task.AIChannels[ch].VirtualName,
                    Values = chData
                });
            }

            if (!string.IsNullOrWhiteSpace(resultVar))
                context.SetVariable(resultVar, waveform);

            // 存盘逻辑
            if (setting.SaveToFile)
            {
                var outputDir = !string.IsNullOrWhiteSpace(setting.OutputDirectory)
                    ? await Evaluator.EvalStringAsync(setting.OutputDirectory, context)
                    : string.Empty;
                var filePath = DaqFileWriter.BuildFilePath(outputDir, taskName + "_AI", "csv");
                string[]? names = new string[channels];
                for (int ch = 0; ch < channels; ch++)
                    names[ch] = task.AIChannels[ch].VirtualName;
                DaqFileWriter.AppendCsv(filePath, data, names, setting.MaxFileSizeMB, context.LogAction);
            }

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"{channels} ch × {samples} samples"
                }
            };
        }
        catch (TimeoutException ex)
        {
            return ErrorResult(ex.Message, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
