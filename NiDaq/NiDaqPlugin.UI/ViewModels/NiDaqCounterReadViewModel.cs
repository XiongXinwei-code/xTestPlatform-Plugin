using NiDaq.Models;
namespace NiDaq.UI.ViewModels;
public sealed class NiDaqCounterReadViewModel : AoViewModelBase<NiDaqCounterReadSetting>
{
    public string TaskName { get => Setting.TaskName; set => Change(() => Setting.TaskName = value); }
    public int TimeoutMs { get => Setting.TimeoutMs; set => Change(() => Setting.TimeoutMs = value); }
    public string ResultVariable { get => Setting.ResultVariable; set => Change(() => Setting.ResultVariable = value); }
}
