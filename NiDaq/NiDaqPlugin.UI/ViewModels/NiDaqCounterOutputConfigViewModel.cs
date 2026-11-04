using NiDaq.Models;
namespace NiDaq.UI.ViewModels;
public sealed class NiDaqCounterOutputConfigViewModel : AoViewModelBase<NiDaqCounterOutputConfigSetting>
{
    public string TaskName { get => Setting.TaskName; set => Change(() => Setting.TaskName = value); }
    public int TimeoutMs { get => Setting.TimeoutMs; set => Change(() => Setting.TimeoutMs = value); }
    public string CounterChannel { get => Setting.CounterChannel; set => Change(() => Setting.CounterChannel = value); }
    public string OutputTerminal { get => Setting.OutputTerminal; set => Change(() => Setting.OutputTerminal = value); }
    public double Frequency { get => Setting.Frequency; set => Change(() => Setting.Frequency = value); }
    public double DutyCycle { get => Setting.DutyCycle; set => Change(() => Setting.DutyCycle = value); }
    public double InitialDelaySeconds { get => Setting.InitialDelaySeconds; set => Change(() => Setting.InitialDelaySeconds = value); }
    public CounterIdleState IdleState { get => Setting.IdleState; set => Change(() => Setting.IdleState = value); }
    public Array IdleStateValues => Enum.GetValues<CounterIdleState>();
    public AiSampleMode SampleMode { get => Setting.SampleMode; set => Change(() => Setting.SampleMode = value); }
    public Array SampleModeValues => Enum.GetValues<AiSampleMode>();
    public int PulseCount { get => Setting.PulseCount; set => Change(() => Setting.PulseCount = value); }
    public bool UseTrigger { get => Setting.UseTrigger; set => Change(() => Setting.UseTrigger = value); }
    public string TriggerSource { get => Setting.TriggerSource; set => Change(() => Setting.TriggerSource = value); }
    public TriggerEdge TriggerEdge { get => Setting.TriggerEdge; set => Change(() => Setting.TriggerEdge = value); }
    public Array TriggerEdgeValues => Enum.GetValues<TriggerEdge>();
}
