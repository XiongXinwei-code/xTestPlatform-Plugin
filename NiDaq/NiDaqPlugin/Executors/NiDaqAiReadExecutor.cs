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
            var taskName = await Evaluator.EvaluateAsync<string>(setting.TaskName, context) ?? setting.TaskName;
            var resultVar = await Evaluator.EvaluateAsync<string>(setting.ResultVariable, context) ?? setting.ResultVariable;

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            var taskObj = context.GetVariable(taskName);
            if (taskObj is not DaqTask task)
                return ErrorResult($"未找到任务 '{taskName}'");

            var reader = new AnalogMultiChannelReader(task.Stream);
            task.Stream.Timeout = setting.ReadTimeoutMs > 0 ? setting.ReadTimeoutMs : -1;
            int samplesToRead = setting.SamplesToRead > 0 ? setting.SamplesToRead : task.Stream.AvailableSamplesPerChannel > 0 ? (int)task.Stream.AvailableSamplesPerChannel : 100;
            double[,] data = reader.ReadMultiSample(samplesToRead);

            int channels = data.GetLength(0);
            int samples = data.GetLength(1);

            context.SetVariable(resultVar, data);

            // 存盘逻辑
            if (setting.SaveToFile)
            {
                var outputDir = !string.IsNullOrWhiteSpace(setting.OutputDirectory)
                    ? (await Evaluator.EvaluateAsync<string>(setting.OutputDirectory, context) ?? setting.OutputDirectory)
                    : string.Empty;
                var filePath = DaqFileWriter.BuildFilePath(outputDir, taskName + "_AI", "csv");
                string[]? names = new string[channels];
                for (int ch = 0; ch < channels; ch++)
                    names[ch] = task.AIChannels[ch].VirtualName;
                DaqFileWriter.AppendCsv(filePath, data, names, setting.MaxFileSizeMB, context.LogAction);
            }

            // 自定义事件：将采集数据构造为 WaveformData 发送到界面
            if (setting.EnableCustomEvent && !string.IsNullOrWhiteSpace(setting.CustomEventName))
            {
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
                context.RaiseCustomEvent(setting.CustomEventName, waveform);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult($"AI 读取失败：{ex.Message}");
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
