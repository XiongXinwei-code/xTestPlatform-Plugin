using NationalInstruments.DAQmx;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using DaqTask = NationalInstruments.DAQmx.Task;
using PluginCommon;
namespace NiDaq.Helpers;

internal static class DaqTaskFactory
{
    public static void ConfigureAnalogOutput(DaqTaskResource owner, AoOutputPlan plan)
    {
        var task = owner.AddTask();
        AnalogMultiChannelWriter? writer = null;
        owner.ConfigureAnalogOutput(plan, values => writer!.WriteSingleSample(false, values), task.Start, task.Stop, () =>
        {
            // 两种调用者均为 owner 的真实 worker；正常事务和清理各自使用剩余期限。
            task.Stream.Timeout = owner.RemainingNativeTimeoutMs();
            writer ??= new AnalogMultiChannelWriter(task.Stream);
            _ = owner.RemainingNativeTimeoutMs();
        });
        foreach (var channel in plan.Channels)
        {
            owner.EnsureOperationActive();
            task.AOChannels.CreateVoltageChannel(channel.PhysicalChannel, channel.ColumnName, channel.MinValue, channel.MaxValue, AOVoltageUnits.Volts);
        }
        // 不配置采样时钟：此首版仅按需单点输出，不承诺多通道电气同时更新。
    }
    public static void ConfigureAi(DaqTaskResource owner, DaqTask task, IEnumerable<NiDaqAiChannel> channels)
    {
        foreach (var ch in channels)
        {
            if (!double.IsFinite(ch.MinValue) || !double.IsFinite(ch.MaxValue) || ch.MinValue >= ch.MaxValue) throw new ArgumentException("AI 电压范围必须有限且最小值小于最大值");
            var terminal = ch.Terminal switch { AiTerminalConfig.RSE => AITerminalConfiguration.Rse, AiTerminalConfig.NRSE => AITerminalConfiguration.Nrse, AiTerminalConfig.Pseudodifferential => AITerminalConfiguration.Pseudodifferential, AiTerminalConfig.Differential => AITerminalConfiguration.Differential, _ => throw new ArgumentOutOfRangeException(nameof(ch.Terminal)) };
            owner.EnsureOperationActive();
            task.AIChannels.CreateVoltageChannel(ch.PhysicalChannel, ch.ColumnName, terminal, ch.MinValue, ch.MaxValue, AIVoltageUnits.Volts);
        }
        owner.EnsureOperationActive();
        if (task.AIChannels.Count == 0) throw new ArgumentException("AI 通道列表不能为空");
    }
    public static async System.Threading.Tasks.Task<DaqTaskResource> CreateAndRegisterAsync(xTestPlatform.Core.Engine.IExecutionContext context, string name, Action<DaqTaskResource> configure, CancellationToken ct, DaqTaskResource? ownedResource = null, IEnumerable<string>? physicalChannels = null, int timeoutMs = 10000, ExecutionEpoch? epoch = null)
    {
        ct.ThrowIfCancellationRequested();
        epoch ??= ExecutionEpoch.Capture(context.Resources, "NiDaq");
        epoch.ThrowIfExpired();
        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        await NiDaqTaskRegistry.WaitUntilAbsentAsync(context, name, RemainingBudget(startedAt, timeoutMs), ct).ConfigureAwait(false);
        epoch.ThrowIfExpired();
        var resource = ownedResource ?? new DaqTaskResource(ex => context.Log(LogLevel.Error, $"DAQ {name} 收尾未确认", ex.ToString()), physicalChannels);
        resource.ExecutionCheck = epoch.ThrowIfExpired;
        bool registered = false;
        try
        {
            // 在任何 SDK 配置操作前登记 owner；TaskStop 能发现并冻结建立期资源。
            ct.ThrowIfCancellationRequested(); epoch.ThrowIfExpired();
            NiDaqTaskRegistry.Register(context, name, resource);
            registered = true;
            epoch.ThrowIfExpired();
            await resource.ConfigureAsync(() => { epoch.ThrowIfExpired(); configure(resource); epoch.ThrowIfExpired(); }, ct, RemainingBudget(startedAt, timeoutMs)).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            epoch.ThrowIfExpired();
            NiDaqTaskRegistry.EnsureCurrent(context, name, resource, ct);
            return resource;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 取消只结束调用方等待。真实建立/清理仍由已登记 owner 持有，不能在取消路径
            // 用 None 额外等待剩余期限，亦不能把用户中断包装成清理超时 Error。
            resource.Dispose();
            throw;
        }
        catch (Exception openingError)
        {
            try
            {
                resource.Dispose();
                if (!resource.IsClosed) await resource.CloseAsync(RemainingBudget(startedAt, timeoutMs), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupError) { throw new AggregateException("DAQ 配置失败且清理未确认，保留已登记 owner，请显式 TaskStop 重试", openingError, cleanupError); }
            if (registered) NiDaqTaskRegistry.RemoveClosed(context, name, resource);
            throw;
        }
    }
    public static int RemainingBudget(long startedAt, int timeoutMs)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        double remaining = timeoutMs - System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        if (remaining <= 0) throw new TimeoutException("DAQ 配置/操作总期限已到，不另加固定收尾等待");
        return (int)Math.Ceiling(remaining);
    }
    public static void ConfigureEncoder(DaqTaskResource owner, DaqTask task, string channel, string name, EncoderDecodingType decoding, bool zIndex, int ppr)
    {
        if (ppr <= 0) throw new ArgumentOutOfRangeException(nameof(ppr));
        var type = decoding switch { EncoderDecodingType.X1 => CIEncoderDecodingType.X1, EncoderDecodingType.X2 => CIEncoderDecodingType.X2, EncoderDecodingType.X4 => CIEncoderDecodingType.X4, _ => throw new ArgumentOutOfRangeException(nameof(decoding)) };
        owner.EnsureOperationActive();
        task.CIChannels.CreateAngularEncoderChannel(channel, name, type, zIndex, 0, CIEncoderZIndexPhase.AHighBHigh, ppr, 0, CIAngularEncoderUnits.Ticks);
    }
    public static void ConfigureClock(DaqTaskResource owner, DaqTask task, string source, double rate, AiSampleMode mode, int samples)
    {
        if (!double.IsFinite(rate) || rate <= 0 || samples <= 0) throw new ArgumentException("采样率必须有限正数，采样数必须大于 0");
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        owner.EnsureOperationActive();
        task.Timing.ConfigureSampleClock(source, rate, SampleClockActiveEdge.Rising, mode == AiSampleMode.ContinuousSamples ? SampleQuantityMode.ContinuousSamples : SampleQuantityMode.FiniteSamples, samples);
    }
    public static void ConfigureTrigger(DaqTaskResource owner, DaqTask task, bool enabled, string source, TriggerEdge edge)
    {
        if (!enabled) return;
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("触发已启用，触发源不能为空");
        if (!Enum.IsDefined(edge)) throw new ArgumentOutOfRangeException(nameof(edge));
        owner.EnsureOperationActive();
        task.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger(source, edge == TriggerEdge.Falling ? DigitalEdgeStartTriggerEdge.Falling : DigitalEdgeStartTriggerEdge.Rising);
    }
}
