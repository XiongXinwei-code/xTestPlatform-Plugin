using NiDaq.Models;

namespace NiDaq.UI.ViewModels;

public sealed class NiDaqAoWriteSingleViewModel : AoViewModelBase<NiDaqAoWriteSingleSetting>
{
    public string TaskName { get => Setting.TaskName; set => Change(() => Setting.TaskName = value); }
    public string Values { get => Setting.Values; set => Change(() => Setting.Values = value); }
    public int TimeoutMs { get => Setting.TimeoutMs; set => Change(() => Setting.TimeoutMs = value); }
}
