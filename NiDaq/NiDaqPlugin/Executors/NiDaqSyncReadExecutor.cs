using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqSyncReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqSyncReadPlugin().CreateSerializer();
        var setting = (NiDaqSyncReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

            // 读取 AI 数据
            var aiReader = new AnalogMultiChannelReader(task.Stream);
            task.Stream.Timeout = setting.ReadTimeoutMs > 0 ? setting.ReadTimeoutMs : -1;
            int samplesToRead = setting.SamplesToRead > 0 ? setting.SamplesToRead : 100;
            double[,] aiData = aiReader.ReadMultiSample(samplesToRead);

            int aiChannels = aiData.GetLength(0);
            int samples = aiData.GetLength(1);

            context.SetVariable(resultVar, aiData);

            // 读取编码器数据
            var ciReader = new CounterSingleChannelReader(task.Stream);
            double encoderValue = ciReader.ReadSingleSampleDouble();
            context.SetVariable($"{resultVar}_Encoder", encoderValue);

            // 存盘逻辑：将位置(编码器)与模拟量数据合并写入同一文件
            if (setting.SaveToFile)
            {
                var outputDir = !string.IsNullOrWhiteSpace(setting.OutputDirectory)
                    ? (await Evaluator.EvaluateAsync<string>(setting.OutputDirectory, context) ?? setting.OutputDirectory)
                    : string.Empty;

                var filePath = DaqFileWriter.BuildFilePath(outputDir, taskName + "_Sync", "csv");
                string[] aiNames = new string[aiChannels];
                for (int ch = 0; ch < aiChannels; ch++)
                    aiNames[ch] = task.AIChannels[ch].VirtualName;
                DaqFileWriter.AppendSyncCsv(filePath, aiData, aiNames, encoderValue, setting.MaxFileSizeMB, context.LogAction);
            }

            // 自定义事件：将采集数据构造为 WaveformData 发送到界面
            if (setting.EnableCustomEvent && !string.IsNullOrWhiteSpace(setting.CustomEventName))
            {
                var waveform = new WaveformData
                {
                    TaskID = taskName,
                    SampleRate = task.Timing.SampleClockRate,
                    StartTime = DateTime.Now,
                    Channels = new List<ChannelData>(aiChannels + 1)
                };
                for (int ch = 0; ch < aiChannels; ch++)
                {
                    var chData = new double[samples];
                    for (int s = 0; s < samples; s++)
                        chData[s] = aiData[ch, s];
                    waveform.Channels.Add(new ChannelData
                    {
                        Channel = task.AIChannels[ch].VirtualName,
                        Values = chData
                    });
                }
                waveform.Channels.Add(new ChannelData
                {
                    Channel = "Encoder",
                    Values = [encoderValue]
                });
                context.RaiseCustomEvent(setting.CustomEventName, waveform);
            }

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"Sync: {aiChannels} AI ch × {samples} samples + encoder"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult($"同步读取失败：{ex.Message}");
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
