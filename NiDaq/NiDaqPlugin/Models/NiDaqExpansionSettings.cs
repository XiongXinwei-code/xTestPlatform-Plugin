using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[JsonConverter(typeof(JsonStringEnumConverter<CounterInputMode>))]
public enum CounterInputMode { CountEdges, Frequency }
[JsonConverter(typeof(JsonStringEnumConverter<CounterDirection>))]
public enum CounterDirection { Up, Down, ExternallyControlled }
[JsonConverter(typeof(JsonStringEnumConverter<CounterIdleState>))]
public enum CounterIdleState { Low, High }
[JsonConverter(typeof(JsonStringEnumConverter<EncoderZPhase>))]
public enum EncoderZPhase { AHighBHigh, AHighBLow, ALowBHigh, ALowBLow }

[MessagePackObject(true)]
public class DaqNamedSetting
{
    [ExpressionField] public string TaskName { get; set; } = "\"DaqTask1\"";
    public int TimeoutMs { get; set; } = 10000;
}

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiDaqCounterInputConfigSetting : DaqNamedSetting
{
    [ExpressionField] public string CounterChannel { get; set; } = "\"Dev1/ctr0\"";
    public CounterInputMode Mode { get; set; } = CounterInputMode.CountEdges;
    public TriggerEdge Edge { get; set; } = TriggerEdge.Rising;
    public uint InitialCount { get; set; }
    public CounterDirection Direction { get; set; } = CounterDirection.Up;
    public string InputTerminal { get; set; } = "";
    public double MinimumFrequency { get; set; } = 1;
    public double MaximumFrequency { get; set; } = 1000000;
}

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiDaqCounterReadSetting : DaqNamedSetting
{
    [VariablePathField] public string ResultVariable { get; set; } = "";
}

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiDaqCounterOutputConfigSetting : DaqNamedSetting
{
    [ExpressionField] public string CounterChannel { get; set; } = "\"Dev1/ctr0\"";
    public string OutputTerminal { get; set; } = "";
    public double Frequency { get; set; } = 1000;
    public double DutyCycle { get; set; } = 0.5;
    public double InitialDelaySeconds { get; set; }
    public CounterIdleState IdleState { get; set; } = CounterIdleState.Low;
    public AiSampleMode SampleMode { get; set; } = AiSampleMode.FiniteSamples;
    public int PulseCount { get; set; } = 100;
    public bool UseTrigger { get; set; }
    public string TriggerSource { get; set; } = "";
    public TriggerEdge TriggerEdge { get; set; } = TriggerEdge.Rising;
}

[MessagePackObject(true)]
public class DaqDigitalConfigSetting : DaqNamedSetting
{
    [ExpressionField] public string Channels { get; set; } = "\"Dev1/port0\"";
    public bool UseSampleClock { get; set; }
    public string ClockSource { get; set; } = "";
    public double SampleRate { get; set; } = 1000;
    public AiSampleMode SampleMode { get; set; } = AiSampleMode.FiniteSamples;
    public int SamplesPerChannel { get; set; } = 1000;
    public bool UseTrigger { get; set; }
    public string TriggerSource { get; set; } = "";
    public TriggerEdge TriggerEdge { get; set; } = TriggerEdge.Rising;
}
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiDaqDiConfigSetting : DaqDigitalConfigSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiDaqDoConfigSetting : DaqDigitalConfigSetting { }

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiDaqDigitalReadSetting : DaqNamedSetting
{
    public int SamplesPerChannel { get; set; } = 1;
    [VariablePathField] public string ResultVariable { get; set; } = "";
}
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiDaqDigitalWriteSetting : DaqNamedSetting
{
    [ExpressionField] public string Values { get; set; } = "0u";
}
