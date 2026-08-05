using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqAiConfigExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqAiConfigPlugin().CreateSerializer();
        var setting = (NiDaqAiConfigSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvaluateAsync<string>(setting.TaskName, context) ?? setting.TaskName;

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            if (setting.Channels.Count == 0)
                return ErrorResult("AI 通道列表为空");

            // 若已存在同名任务（序列异常终止未销毁），先销毁旧任务
            var existingTask = context.GetVariable(taskName);
            if (existingTask is DaqTask oldTask)
            {
                try { oldTask.Dispose(); } catch { /* 忽略销毁异常 */ }
                context.LogAction?.Invoke($"NI DAQ 任务 '{taskName}' 检测到已有任务，已自动销毁旧任务");
            }

            var task = new DaqTask(taskName);

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

            var sampleMode = setting.SampleMode == AiSampleMode.ContinuousSamples
                ? SampleQuantityMode.ContinuousSamples
                : SampleQuantityMode.FiniteSamples;

            var clockSource = string.IsNullOrWhiteSpace(setting.ClockSource) ? "" : setting.ClockSource;

            task.Timing.ConfigureSampleClock(clockSource, setting.SampleRate,
                SampleClockActiveEdge.Rising, sampleMode, setting.SamplesPerChannel);

            if (setting.UseTrigger && !string.IsNullOrWhiteSpace(setting.TriggerSource))
            {
                var edge = setting.TriggerEdge == TriggerEdge.Falling
                    ? DigitalEdgeStartTriggerEdge.Falling
                    : DigitalEdgeStartTriggerEdge.Rising;
                task.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger(setting.TriggerSource, edge);
            }

            context.SetVariable(taskName, task);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"AI 任务 '{taskName}' 已配置 ({setting.Channels.Count} 通道, {setting.SampleRate} Hz)"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult($"AI 配置失败：{ex.Message}");
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
