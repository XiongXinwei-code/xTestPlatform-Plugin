using Ethernet.DoIP.Models;

namespace Ethernet.UI.ViewModels;

public class DoipVehicleDiscoveryViewModel : EthernetViewModelBase<DoipVehicleDiscoverySetting>
{
    public string BroadcastAddress
    {
        get => Setting?.BroadcastAddress ?? "\"255.255.255.255\"";
        set { if (Setting == null || Setting.BroadcastAddress == value) return; Setting.BroadcastAddress = value; OnPropertyChanged(); QueueSave(); }
    }
    public int Port
    {
        get => Setting?.Port ?? 13400;
        set { if (Setting == null || Setting.Port == value) return; Setting.Port = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 3000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ResultVariable
    {
        get => Setting?.ResultVariable ?? string.Empty;
        set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableLog
    {
        get => Setting?.EnableLog ?? true;
        set { if (Setting == null || Setting.EnableLog == value) return; Setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }
}
