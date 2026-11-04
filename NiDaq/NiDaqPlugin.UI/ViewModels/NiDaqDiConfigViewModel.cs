using NiDaq.Models;
namespace NiDaq.UI.ViewModels;
public sealed class NiDaqDiConfigViewModel : AoViewModelBase<NiDaqDiConfigSetting>
{
    public string TaskName { get => Setting.TaskName; set => Change(() => Setting.TaskName = value); }
    public int TimeoutMs { get => Setting.TimeoutMs; set => Change(() => Setting.TimeoutMs = value); }
    public string Channels { get => Setting.Channels; set => Change(() => Setting.Channels = value); }
    public bool UseSampleClock { get => Setting.UseSampleClock; set => Change(() => Setting.UseSampleClock = value); }
    public string ClockSource { get => Setting.ClockSource; set => Change(() => Setting.ClockSource = value); }
    public double SampleRate { get => Setting.SampleRate; set => Change(() => Setting.SampleRate = value); }
    public AiSampleMode SampleMode { get => Setting.SampleMode; set => Change(() => Setting.SampleMode = value); }
    public Array SampleModeValues => Enum.GetValues<AiSampleMode>();
    public int SamplesPerChannel { get => Setting.SamplesPerChannel; set => Change(() => Setting.SamplesPerChannel = value); }
    public bool UseTrigger { get => Setting.UseTrigger; set => Change(() => Setting.UseTrigger = value); }
    public string TriggerSource { get => Setting.TriggerSource; set => Change(() => Setting.TriggerSource = value); }
    public TriggerEdge TriggerEdge { get => Setting.TriggerEdge; set => Change(() => Setting.TriggerEdge = value); }
    public Array TriggerEdgeValues => Enum.GetValues<TriggerEdge>();
}
