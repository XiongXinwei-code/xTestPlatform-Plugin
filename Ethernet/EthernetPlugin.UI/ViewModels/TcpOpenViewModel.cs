using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public class TcpOpenViewModel : EthernetViewModelBase<TcpOpenSetting>
{
    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? "\"TCP1\"";
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public string RemoteHost
    {
        get => Setting?.RemoteHost ?? "\"192.168.1.1\"";
        set { if (Setting == null || Setting.RemoteHost == value) return; Setting.RemoteHost = value; OnPropertyChanged(); QueueSave(); }
    }
    public string RemotePort
    {
        get => Setting?.RemotePort ?? "\"13400\"";
        set { if (Setting == null || Setting.RemotePort == value) return; Setting.RemotePort = value; OnPropertyChanged(); QueueSave(); }
    }
    public int ConnectTimeoutMs
    {
        get => Setting?.ConnectTimeoutMs ?? 3000;
        set { if (Setting == null || Setting.ConnectTimeoutMs == value) return; Setting.ConnectTimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableLog
    {
        get => Setting?.EnableLog ?? true;
        set { if (Setting == null || Setting.EnableLog == value) return; Setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }
}
