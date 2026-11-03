using NationalInstruments.DAQmx;
using NiDaq.Models;

namespace NiDaq.Helpers;

internal interface IDaqExpansionBackend
{
    void Configure(DaqTaskResource resource, DaqNamedSetting setting, string[] physicalChannels);
    object ReadCounter(DaqTaskResource resource);
    uint[,] ReadDigital(DaqTaskResource resource, int samples);
    void WriteDigital(DaqTaskResource resource, uint[,] values);
}

internal sealed class DaqExpansionBackend : IDaqExpansionBackend
{
    public void Configure(DaqTaskResource resource, DaqNamedSetting setting, string[] physicalChannels)
    {
        var task = resource.AddTask();
        switch (setting)
        {
            case NiDaqCounterInputConfigSetting s:
                resource.SetKind(s.Mode == CounterInputMode.CountEdges ? DaqTaskKind.CounterEdges : DaqTaskKind.CounterFrequency);
                resource.EnsureOperationActive();
                if (s.Mode == CounterInputMode.CountEdges)
                {
                    var direction = s.Direction switch { CounterDirection.Up => CICountEdgesCountDirection.Up, CounterDirection.Down => CICountEdgesCountDirection.Down, CounterDirection.ExternallyControlled => CICountEdgesCountDirection.ExternallyControlled, _ => throw new ArgumentException("计数方向无效") };
                    var channel = task.CIChannels.CreateCountEdgesChannel(physicalChannels[0], "Counter", s.Edge == TriggerEdge.Rising ? CICountEdgesActiveEdge.Rising : CICountEdgesActiveEdge.Falling, s.InitialCount, direction);
                    if (!string.IsNullOrWhiteSpace(s.InputTerminal)) { resource.EnsureOperationActive(); channel.CountEdgesTerminal = s.InputTerminal.Trim(); }
                }
                else
                {
                    var channel = task.CIChannels.CreateFrequencyChannel(physicalChannels[0], "Counter", s.MinimumFrequency, s.MaximumFrequency,
                        s.Edge == TriggerEdge.Rising ? CIFrequencyStartingEdge.Rising : CIFrequencyStartingEdge.Falling, CIFrequencyMeasurementMethod.LowFrequencyOneCounter, 0.001, 4, CIFrequencyUnits.Hertz);
                    if (!string.IsNullOrWhiteSpace(s.InputTerminal)) { resource.EnsureOperationActive(); channel.FrequencyTerminal = s.InputTerminal.Trim(); }
                }
                break;
            case NiDaqCounterOutputConfigSetting s:
                resource.SetKind(DaqTaskKind.CounterOutput);
                resource.EnsureOperationActive();
                var output = task.COChannels.CreatePulseChannelFrequency(physicalChannels[0], "Pulse", COPulseFrequencyUnits.Hertz,
                    s.IdleState == CounterIdleState.Low ? COPulseIdleState.Low : COPulseIdleState.High, s.InitialDelaySeconds, s.Frequency, s.DutyCycle);
                if (!string.IsNullOrWhiteSpace(s.OutputTerminal)) { resource.EnsureOperationActive(); output.PulseTerminal = s.OutputTerminal.Trim(); }
                resource.EnsureOperationActive();
                task.Timing.ConfigureImplicit(s.SampleMode == AiSampleMode.FiniteSamples ? SampleQuantityMode.FiniteSamples : SampleQuantityMode.ContinuousSamples, s.PulseCount);
                DaqTaskFactory.ConfigureTrigger(resource, task, s.UseTrigger, s.TriggerSource, s.TriggerEdge);
                break;
            case DaqDigitalConfigSetting s:
                bool isInput = s is NiDaqDiConfigSetting;
                resource.SetKind(isInput ? DaqTaskKind.DigitalInput : DaqTaskKind.DigitalOutput);
                var widths = new int[physicalChannels.Length];
                for (int i = 0; i < physicalChannels.Length; i++)
                {
                    resource.EnsureOperationActive();
                    if (isInput) { var c = task.DIChannels.CreateChannel(physicalChannels[i], "Port" + i, ChannelLineGrouping.OneChannelForAllLines); resource.EnsureOperationActive(); widths[i] = checked((int)c.NumberOfLines); }
                    else { var c = task.DOChannels.CreateChannel(physicalChannels[i], "Port" + i, ChannelLineGrouping.OneChannelForAllLines); resource.EnsureOperationActive(); widths[i] = checked((int)c.NumberOfLines); }
                }
                resource.SetDigitalLayout(physicalChannels, widths, s.UseSampleClock);
                if (s.UseSampleClock) DaqTaskFactory.ConfigureClock(resource, task, s.ClockSource, s.SampleRate, s.SampleMode, s.SamplesPerChannel);
                DaqTaskFactory.ConfigureTrigger(resource, task, s.UseTrigger, s.TriggerSource, s.TriggerEdge);
                break;
            default: throw new ArgumentException("未知 DAQ 配置类型");
        }
    }
    public object ReadCounter(DaqTaskResource resource)
    {
        var task = resource.Primary;
        task.Stream.Timeout = resource.RemainingNativeTimeoutMs();
        resource.EnsureOperationActive();
        var reader = new CounterSingleChannelReader(task.Stream);
        if (resource.Kind == DaqTaskKind.CounterEdges) return reader.ReadSingleSampleUInt32();
        return reader.ReadSingleSampleDouble();
    }
    public uint[,] ReadDigital(DaqTaskResource resource, int samples)
    {
        var task = resource.Primary;
        task.Stream.Timeout = resource.RemainingNativeTimeoutMs(); resource.EnsureOperationActive();
        var reader = new DigitalMultiChannelReader(task.Stream);
        if (resource.DigitalBuffered) return reader.ReadMultiSamplePortUInt32(samples);
        var values = reader.ReadSingleSamplePortUInt32();
        var result = new uint[values.Length, 1];
        for (int i = 0; i < values.Length; i++) result[i, 0] = values[i];
        return result;
    }
    public void WriteDigital(DaqTaskResource resource, uint[,] values)
    {
        var task = resource.Primary;
        task.Stream.Timeout = resource.RemainingNativeTimeoutMs(); resource.EnsureOperationActive();
        var writer = new DigitalMultiChannelWriter(task.Stream);
        if (resource.DigitalBuffered) writer.WriteMultiSamplePort(false, values);
        else
        {
            var single = new uint[values.GetLength(0)];
            for (int i = 0; i < single.Length; i++) single[i] = values[i, 0];
            writer.WriteSingleSamplePort(false, single);
        }
    }
}
