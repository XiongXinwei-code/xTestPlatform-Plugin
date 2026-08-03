using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqAiStartExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqAiStartPlugin().CreateSerializer();
        var setting = (NiDaqAiStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvaluateAsync<string>(setting.TaskName, context) ?? setting.TaskName;
            var outputDir = await Evaluator.EvaluateAsync<string>(setting.OutputDirectory, context) ?? setting.OutputDirectory;
            var statPrefix = await Evaluator.EvaluateAsync<string>(setting.StatVariablePrefix, context) ?? setting.StatVariablePrefix;
            var taskKey = $"NiDaqAi_{taskName}";

            if (string.IsNullOrWhiteSpace(outputDir))
                outputDir = Path.GetTempPath();

            Directory.CreateDirectory(outputDir);
            var fileName = $"{taskName}_{DateTime.Now:yyyyMMdd_HHmmss}.tdms";
            var filePath = Path.Combine(outputDir, fileName);

            var daqTask = new DaqTask();
            foreach (var ch in setting.Channels)
            {
                var termConfig = ch.Terminal switch
                {
                    AiTerminalConfig.RSE => AITerminalConfiguration.Rse,
                    AiTerminalConfig.NRSE => AITerminalConfiguration.Nrse,
                    AiTerminalConfig.Pseudodifferential => AITerminalConfiguration.Pseudodifferential,
                    _ => AITerminalConfiguration.Differential
                };
                daqTask.AIChannels.CreateVoltageChannel(
                    ch.PhysicalChannel, ch.ColumnName,
                    termConfig, ch.MinValue, ch.MaxValue, AIVoltageUnits.Volts);
            }

            daqTask.Timing.ConfigureSampleClock("", setting.SampleRate,
                SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples);

            var reader = new AnalogMultiChannelReader(daqTask.Stream);
            var channelNames = setting.Channels.Select(c => c.ColumnName).ToArray();

            var streamTask = new NiDaqAiStreamTask(
                daqTask, reader, channelNames, filePath,
                setting.ReadBatchSize, setting.MaxDurationMs, setting.ExportFormat);

            streamTask.Start();

            // 存入 RuntimeData
            context.CurrentStep.RuntimeData[taskKey] = streamTask;
            context.CurrentStep.RuntimeData[$"{taskKey}_FilePath"] = filePath;
            context.CurrentStep.RuntimeData[$"{taskKey}_StatPrefix"] = statPrefix;

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"AI 连续采集已启动: {taskName}"
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
                    Error = new ErrorInfo { Message = $"AI 连续采集启动失败：{ex.Message}" }
                }
            };
        }
    }
}
