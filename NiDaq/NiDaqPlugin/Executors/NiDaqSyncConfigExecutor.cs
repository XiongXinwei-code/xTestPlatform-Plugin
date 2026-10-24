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
    private readonly Action<DaqTaskResource, NiDaqSyncConfigSetting>? _configure;
    private readonly Func<IEnumerable<string>, DaqTaskResource>? _createOwner;
    public NiDaqSyncConfigExecutor() { }
    internal NiDaqSyncConfigExecutor(Action<DaqTaskResource, NiDaqSyncConfigSetting> configure,
        Func<IEnumerable<string>, DaqTaskResource> createOwner) { _configure = configure; _createOwner = createOwner; }

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        DaqTaskResource? configuredOwner = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            var epoch = guard.Epoch;
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new NiDaqSyncConfigPlugin().CreateSerializer();
            var setting = (NiDaqSyncConfigSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var plan = DaqSamplePlan.Create(setting);
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            guard.Check();
            var owner = await DaqTaskFactory.CreateAndRegisterAsync(context, taskName, resource =>
            {
                if (_configure != null) { _configure(resource, setting); return; }
                var ai = resource.AddTask();
                DaqTaskFactory.ConfigureAi(resource, ai, setting.AiChannels);
                DaqTaskFactory.ConfigureClock(resource, ai, setting.ClockSource, setting.SampleRate, setting.SampleMode, setting.SamplesPerChannel);
                DaqTaskFactory.ConfigureTrigger(resource, ai, setting.UseTrigger, setting.TriggerSource, setting.TriggerEdge);
                int index = 0;
                foreach (var enc in setting.EncoderChannels)
                {
                    guard.Check();
                    var counter = resource.AddTask();
                    DaqTaskFactory.ConfigureEncoder(resource, counter, enc.CounterChannel, "", enc.DecodingType, enc.ZIndexEnable, enc.PulsesPerRevolution);
                    DaqTaskFactory.ConfigureClock(resource, counter, plan.CounterClockSource, setting.SampleRate, setting.SampleMode, setting.SamplesPerChannel);
                    index++;
                }
                resource.CounterNames = setting.EncoderChannels.Select((enc, i) => string.IsNullOrWhiteSpace(enc.ColumnName) ? "Encoder" + i : enc.ColumnName).ToArray();
            }, cancellationToken, ownedResource: _createOwner?.Invoke(setting.AiChannels.Select(c => c.PhysicalChannel).Concat(setting.EncoderChannels.Select(c => c.CounterChannel))), physicalChannels: setting.AiChannels.Select(c => c.PhysicalChannel).Concat(setting.EncoderChannels.Select(c => c.CounterChannel)), timeoutMs: setting.TimeoutMs, epoch: epoch);
            configuredOwner = owner;
            context.Log("DAQ 同步方案: AI 和每路 CI 独立任务，CI 时钟=" + plan.CounterClockSource + "；路由已由驱动 Verify，真实设备同步资格仍需实测");
            guard.EnsureCurrent(taskName, owner);
            epoch.ThrowIfExpired();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"同步任务 '{taskName}' 已配置 ({setting.AiChannels.Count} AI + {setting.EncoderChannels.Count} Enc)"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            configuredOwner?.Dispose();
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            configuredOwner?.Dispose();
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
