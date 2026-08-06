using Ethernet.DoIP.Models;

namespace Ethernet.UI.ViewModels;

public class DoipDisconnectViewModel : EthernetViewModelBase<DoipDisconnectSetting>
{
    public string SessionName
    {
        get => Setting?.SessionName ?? "\"DOIP1\"";
        set { if (Setting == null || Setting.SessionName == value) return; Setting.SessionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableLog
    {
        get => Setting?.EnableLog ?? true;
        set { if (Setting == null || Setting.EnableLog == value) return; Setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }
}
