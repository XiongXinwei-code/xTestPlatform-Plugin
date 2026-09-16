using Ethernet.DoIP.Models;

namespace Ethernet.UI.ViewModels;

public class DoipDisconnectViewModel : EthernetViewModelBase<DoipDisconnectSetting>
{
    public string SessionName
    {
        get => Setting?.SessionName ?? "\"DOIP1\"";
        set { if (Setting == null || Setting.SessionName == value) return; Setting.SessionName = value; OnPropertyChanged(); QueueSave(); }
    }
}
