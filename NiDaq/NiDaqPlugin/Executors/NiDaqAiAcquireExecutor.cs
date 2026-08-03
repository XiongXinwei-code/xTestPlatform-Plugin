using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqAiAcquireExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqAiAcquirePlugin().CreateSerializer();
        var setting = (NiDaqAiAcquireSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var prefix = await Evaluator.EvaluateAsync<string>(setting.ResultVariablePrefix, context) ?? setting.ResultVariablePrefix;

            if (setting.Channels.Count == 0)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "AI 通道列表为空" }
                    }
                };
            }

            using var task = new DaqTask();

            foreach (var ch in setting.Channels)
            {
                var termConfig = ch.Terminal switch
                {
                    AiTerminalConfig.RSE => AITerminalConfiguration.Rse,
                    AiTerminalConfig.NRSE => AITerminalConfiguration.Nrse,
                    AiTerminalConfig.Pseudodifferential => AITerminalConfiguration.Pseudodifferential,
                    _ => AITerminalConfiguration.Differential
                };

                task.AIChannels.CreateVoltageChannel(
                    ch.PhysicalChannel, ch.ColumnName,
                    termConfig, ch.MinValue, ch.MaxValue, AIVoltageUnits.Volts);
            }

            task.Timing.ConfigureSampleClock("", setting.SampleRate,
                SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples, setting.SamplesPerChannel);

            var reader = new AnalogMultiChannelReader(task.Stream);
            task.Start();
            double[,] data = reader.ReadMultiSample(setting.SamplesPerChannel);
            task.Stop();

            int channels = data.GetLength(0);
            int samples = data.GetLength(1);

            for (int ch = 0; ch < channels; ch++)
            {
                var name = setting.Channels[ch].ColumnName;
                double sum = 0, max = double.MinValue, min = double.MaxValue;
                for (int s = 0; s < samples; s++)
                {
                    double v = data[ch, s];
                    sum += v;
                    if (v > max) max = v;
                    if (v < min) min = v;
                }
                double avg = sum / samples;

                context.SetVariable($"{prefix}_{name}_Avg", avg);
                context.SetVariable($"{prefix}_{name}_Max", max);
                context.SetVariable($"{prefix}_{name}_Min", min);
                context.SetVariable($"{prefix}_{name}_Count", samples);
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
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"AI 采集失败：{ex.Message}" }
                }
            };
        }
    }
}
