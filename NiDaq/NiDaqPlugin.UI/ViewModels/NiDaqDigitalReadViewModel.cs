using NiDaq.Models;
namespace NiDaq.UI.ViewModels;
public sealed class NiDaqDigitalReadViewModel : AoViewModelBase<NiDaqDigitalReadSetting>
{
    public string TaskName { get => Setting.TaskName; set => Change(() => Setting.TaskName = value); }
    public int TimeoutMs { get => Setting.TimeoutMs; set => Change(() => Setting.TimeoutMs = value); }
    public int SamplesPerChannel { get => Setting.SamplesPerChannel; set => Change(() => Setting.SamplesPerChannel = value); }
    public string ResultVariable { get => Setting.ResultVariable; set => Change(() => Setting.ResultVariable = value); }
}
