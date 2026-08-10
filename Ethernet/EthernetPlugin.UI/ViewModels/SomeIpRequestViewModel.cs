using Ethernet.SomeIP.Models;

namespace Ethernet.UI.ViewModels;

public class SomeIpRequestViewModel : EthernetViewModelBase<SomeIpRequestSetting>
{
    private static readonly SomeIpTransport[] Transports = [SomeIpTransport.Udp, SomeIpTransport.Tcp];

    public int TransportIndex
    {
        get => Setting == null ? 0 : Array.IndexOf(Transports, Setting.Transport);
        set { if (Setting == null || value < 0 || value >= Transports.Length) return; Setting.Transport = Transports[value]; OnPropertyChanged(); QueueSave(); }
    }
    public string RemoteHost
    {
        get => Setting?.RemoteHost ?? "\"192.168.1.20\"";
        set { if (Setting == null || Setting.RemoteHost == value) return; Setting.RemoteHost = value; OnPropertyChanged(); QueueSave(); }
    }
    public string RemotePort
    {
        get => Setting?.RemotePort ?? "\"30501\"";
        set { if (Setting == null || Setting.RemotePort == value) return; Setting.RemotePort = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ServiceId
    {
        get => Setting?.ServiceId ?? "\"0x1234\"";
        set { if (Setting == null || Setting.ServiceId == value) return; Setting.ServiceId = value; OnPropertyChanged(); QueueSave(); }
    }
    public string MethodId
    {
        get => Setting?.MethodId ?? "\"0x0001\"";
        set { if (Setting == null || Setting.MethodId == value) return; Setting.MethodId = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ClientId
    {
        get => Setting?.ClientId ?? "\"0x0001\"";
        set { if (Setting == null || Setting.ClientId == value) return; Setting.ClientId = value; OnPropertyChanged(); QueueSave(); }
    }
    public string InterfaceVersion
    {
        get => Setting?.InterfaceVersion ?? "\"0x01\"";
        set { if (Setting == null || Setting.InterfaceVersion == value) return; Setting.InterfaceVersion = value; OnPropertyChanged(); QueueSave(); }
    }
    public string Payload
    {
        get => Setting?.Payload ?? "\"\"";
        set { if (Setting == null || Setting.Payload == value) return; Setting.Payload = value; OnPropertyChanged(); QueueSave(); }
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
