using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public class TcpCloseViewModel : EthernetViewModelBase<TcpCloseSetting>
{
    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? "\"TCP1\"";
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableLog
    {
        get => Setting?.EnableLog ?? true;
        set { if (Setting == null || Setting.EnableLog == value) return; Setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }
}
