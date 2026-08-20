using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqSyncConfigExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqSyncConfigPlugin().CreateSerializer();
        var setting = (NiDaqSyncConfigSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");
            if (setting.AiChannels.Count == 0)
                return ErrorResult("AI 通道列表为空");
            if (setting.EncoderChannels.Count == 0)
                return ErrorResult("编码器通道列表为空");

            // 若已存在同名任务（序列异常终止未销毁），先销毁旧任务
            var existingTask = context.GetVariable(taskName);
            if (existingTask is DaqTask oldTask)
            {
                try { oldTask.Dispose(); } catch { /* 忽略销毁异常 */ }
                context.LogAction?.Invoke($"NI DAQ 任务 '{taskName}' 检测到已有任务，已自动销毁旧任务");
            }

            var task = new DaqTask(taskName);

            // 配置 AI 通道
            foreach (var ch in setting.AiChannels)
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

            // 配置编码器通道
            foreach (var enc in setting.EncoderChannels)
            {
                var decodingType = enc.DecodingType switch
                {
                    EncoderDecodingType.X1 => CIEncoderDecodingType.X1,
                    EncoderDecodingType.X2 => CIEncoderDecodingType.X2,
                    _ => CIEncoderDecodingType.X4
                };
                task.CIChannels.CreateAngularEncoderChannel(
                    enc.CounterChannel, enc.ColumnName,
                    decodingType,
                    enc.ZIndexEnable, 0,
                    CIEncoderZIndexPhase.AHighBHigh,
                    enc.PulsesPerRevolution, 0,
                    CIAngularEncoderUnits.Ticks);
            }

            // 时钟配置
            var sampleMode = setting.SampleMode == AiSampleMode.ContinuousSamples
                ? SampleQuantityMode.ContinuousSamples
                : SampleQuantityMode.FiniteSamples;
            var clockSource = string.IsNullOrWhiteSpace(setting.ClockSource) ? "" : setting.ClockSource;
            task.Timing.ConfigureSampleClock(clockSource, setting.SampleRate,
                SampleClockActiveEdge.Rising, sampleMode, setting.SamplesPerChannel);

            // 触发配置
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
                    Value = $"同步任务 '{taskName}' 已配置 ({setting.AiChannels.Count} AI + {setting.EncoderChannels.Count} Enc)"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult($"同步配置失败：{ex.Message}", ex);
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
