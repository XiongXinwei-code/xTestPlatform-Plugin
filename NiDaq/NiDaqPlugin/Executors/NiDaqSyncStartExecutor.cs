using NationalInstruments.DAQmx;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.Executors;

public sealed class NiDaqSyncStartExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqSyncStartPlugin().CreateSerializer();
        var setting = (NiDaqSyncStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var taskName = await Evaluator.EvaluateAsync<string>(setting.TaskName, context) ?? setting.TaskName;
            var outputDir = await Evaluator.EvaluateAsync<string>(setting.OutputDirectory, context) ?? setting.OutputDirectory;
            var statPrefix = await Evaluator.EvaluateAsync<string>(setting.StatVariablePrefix, context) ?? setting.StatVariablePrefix;
            var taskKey = $"NiDaqSync_{taskName}";

            if (string.IsNullOrWhiteSpace(outputDir))
                outputDir = Path.GetTempPath();
            Directory.CreateDirectory(outputDir);

            var fileName = $"{taskName}_{DateTime.Now:yyyyMMdd_HHmmss}.tdms";
            var filePath = Path.Combine(outputDir, fileName);

            // 创建 AI 任务
            var aiTask = new NationalInstruments.DAQmx.Task();
            foreach (var ch in setting.AiChannels)
            {
                var termConfig = ch.Terminal switch
                {
                    AiTerminalConfig.RSE => AITerminalConfiguration.Rse,
                    AiTerminalConfig.NRSE => AITerminalConfiguration.Nrse,
                    AiTerminalConfig.Pseudodifferential => AITerminalConfiguration.Pseudodifferential,
                    _ => AITerminalConfiguration.Differential
                };
                aiTask.AIChannels.CreateVoltageChannel(
                    ch.PhysicalChannel, ch.ColumnName,
                    termConfig, ch.MinValue, ch.MaxValue, AIVoltageUnits.Volts);
            }

            aiTask.Timing.ConfigureSampleClock("", setting.SampleRate,
                SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples);

            // 触发配置
            if (setting.UseTrigger && !string.IsNullOrWhiteSpace(setting.TriggerSource))
            {
                var edge = setting.TriggerEdge == Models.TriggerEdge.Rising
                    ? DigitalEdgeStartTriggerEdge.Rising
                    : DigitalEdgeStartTriggerEdge.Falling;
                aiTask.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger(setting.TriggerSource, edge);
            }

            // 创建 Counter（编码器）任务
            var ciTask = new NationalInstruments.DAQmx.Task();
            var distancePerPulse = new double[setting.EncoderChannels.Count];
            for (int i = 0; i < setting.EncoderChannels.Count; i++)
            {
                var enc = setting.EncoderChannels[i];
                var decodingType = enc.DecodingType switch
                {
                    EncoderDecodingType.X1 => CIEncoderDecodingType.X1,
                    EncoderDecodingType.X2 => CIEncoderDecodingType.X2,
                    _ => CIEncoderDecodingType.X4
                };
                ciTask.CIChannels.CreateAngularEncoderChannel(
                    enc.CounterChannel, enc.ColumnName,
                    decodingType, enc.ZIndexEnable, 0,
                    CIAngularEncoderUnits.Ticks,
                    enc.PulsesPerRevolution, 0, CIFrequencyMeasurementMethod.LowFrequencyOneCounter);
                distancePerPulse[i] = enc.DistancePerPulse;
            }

            // Counter 使用 AI 的采样时钟实现同步
            string aiSampleClockSource = $"/{aiTask.Devices[0]}/ai/SampleClock";
            ciTask.Timing.ConfigureSampleClock(aiSampleClockSource, setting.SampleRate,
                SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples);

            var aiReader = new AnalogMultiChannelReader(aiTask.Stream);
            var ciReader = new CounterMultiChannelReader(ciTask.Stream);
            var aiNames = setting.AiChannels.Select(c => c.ColumnName).ToArray();
            var encNames = setting.EncoderChannels.Select(c => c.ColumnName).ToArray();

            var syncTask = new NiDaqSyncStreamTask(
                aiTask, ciTask, aiReader, ciReader,
                aiNames, encNames, distancePerPulse,
                filePath, setting.ReadBatchSize, setting.MaxDurationMs, setting.ExportFormat);

            syncTask.Start();

            context.CurrentStep.RuntimeData[taskKey] = syncTask;
            context.CurrentStep.RuntimeData[$"{taskKey}_FilePath"] = filePath;
            context.CurrentStep.RuntimeData[$"{taskKey}_StatPrefix"] = statPrefix;

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"同步采集已启动: {taskName} ({aiNames.Length} AI + {encNames.Length} Enc)"
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
                    Error = new ErrorInfo { Message = $"同步采集启动失败：{ex.Message}" }
                }
            };
        }
    }
}
