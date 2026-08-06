using Ethernet.SomeIP.Models;

namespace Ethernet.UI.ViewModels;

public class SomeIpSdDiscoverViewModel : EthernetViewModelBase<SomeIpSdDiscoverSetting>
{
    public string MulticastAddress
    {
        get => Setting?.MulticastAddress ?? "\"224.244.224.245\"";
        set { if (Setting == null || Setting.MulticastAddress == value) return; Setting.MulticastAddress = value; OnPropertyChanged(); QueueSave(); }
    }
    public int Port
    {
        get => Setting?.Port ?? 30490;
        set { if (Setting == null || Setting.Port == value) return; Setting.Port = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ServiceId
    {
        get => Setting?.ServiceId ?? "\"0xFFFF\"";
        set { if (Setting == null || Setting.ServiceId == value) return; Setting.ServiceId = value; OnPropertyChanged(); QueueSave(); }
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
