using Ethernet.SomeIP.Models;

namespace Ethernet.UI.ViewModels;

public class SomeIpSubscribeViewModel : EthernetViewModelBase<SomeIpSubscribeSetting>
{
    public string LocalPort
    {
        get => Setting?.LocalPort ?? "\"30502\"";
        set { if (Setting == null || Setting.LocalPort == value) return; Setting.LocalPort = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ServiceId
    {
        get => Setting?.ServiceId ?? "\"0x1234\"";
        set { if (Setting == null || Setting.ServiceId == value) return; Setting.ServiceId = value; OnPropertyChanged(); QueueSave(); }
    }
    public string EventId
    {
        get => Setting?.EventId ?? "\"0x8001\"";
        set { if (Setting == null || Setting.EventId == value) return; Setting.EventId = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 5000;
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
