using NiDaq.Models;
namespace NiDaq.UI.ViewModels;
public sealed class NiDaqCounterInputConfigViewModel : AoViewModelBase<NiDaqCounterInputConfigSetting>
{
    public string TaskName { get => Setting.TaskName; set => Change(() => Setting.TaskName = value); }
    public int TimeoutMs { get => Setting.TimeoutMs; set => Change(() => Setting.TimeoutMs = value); }
    public string CounterChannel { get => Setting.CounterChannel; set => Change(() => Setting.CounterChannel = value); }
    public CounterInputMode Mode { get => Setting.Mode; set => Change(() => Setting.Mode = value); }
    public Array ModeValues => Enum.GetValues<CounterInputMode>();
    public TriggerEdge Edge { get => Setting.Edge; set => Change(() => Setting.Edge = value); }
    public Array EdgeValues => Enum.GetValues<TriggerEdge>();
    public uint InitialCount { get => Setting.InitialCount; set => Change(() => Setting.InitialCount = value); }
    public CounterDirection Direction { get => Setting.Direction; set => Change(() => Setting.Direction = value); }
    public Array DirectionValues => Enum.GetValues<CounterDirection>();
    public string InputTerminal { get => Setting.InputTerminal; set => Change(() => Setting.InputTerminal = value); }
    public double MinimumFrequency { get => Setting.MinimumFrequency; set => Change(() => Setting.MinimumFrequency = value); }
    public double MaximumFrequency { get => Setting.MaximumFrequency; set => Change(() => Setting.MaximumFrequency = value); }
}
