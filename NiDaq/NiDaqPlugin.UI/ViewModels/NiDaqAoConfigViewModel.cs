using System.Collections.ObjectModel;
using NiDaq.Models;

namespace NiDaq.UI.ViewModels;

public sealed class NiDaqAoConfigViewModel : AoViewModelBase<NiDaqAoConfigSetting>
{
    public string TaskName { get => Setting.TaskName; set => Change(() => Setting.TaskName = value); }
    public string InitialValues { get => Setting.InitialValues; set => Change(() => Setting.InitialValues = value); }
    public AoStopPolicy StopPolicy { get => Setting.StopPolicy; set => Change(() => Setting.StopPolicy = value); }
    public string FinalValues { get => Setting.FinalValues; set => Change(() => Setting.FinalValues = value); }
    public int CleanupTimeoutMs { get => Setting.CleanupTimeoutMs; set => Change(() => Setting.CleanupTimeoutMs = value); }
    public int TimeoutMs { get => Setting.TimeoutMs; set => Change(() => Setting.TimeoutMs = value); }
    public ObservableCollection<NiDaqAoChannel> Channels => Setting.Channels;
    public AoStopPolicy[] StopPolicies { get; } = Enum.GetValues<AoStopPolicy>();
    protected override void TrackCollections(Action<System.Collections.IEnumerable> track) => track(Channels);
    public void AddChannel() => Channels.Add(new NiDaqAoChannel());
    public void RemoveChannel(NiDaqAoChannel channel) => Channels.Remove(channel);
}
