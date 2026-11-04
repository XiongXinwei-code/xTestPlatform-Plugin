using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public sealed class UdpOpenViewModel : EthernetViewModelBase<UdpOpenSetting>
{
    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? "\"UDP1\"";
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 3000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public string LocalAddress
    {
        get => Setting?.LocalAddress ?? "\"0.0.0.0\"";
        set { if (Setting == null || Setting.LocalAddress == value) return; Setting.LocalAddress = value; OnPropertyChanged(); QueueSave(); }
    }
    public int LocalPort
    {
        get => Setting?.LocalPort ?? 30490;
        set { if (Setting == null || Setting.LocalPort == value) return; Setting.LocalPort = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableBroadcast
    {
        get => Setting?.EnableBroadcast ?? false;
        set { if (Setting == null || Setting.EnableBroadcast == value) return; Setting.EnableBroadcast = value; OnPropertyChanged(); QueueSave(); }
    }
    public string LocalEndpointVariable
    {
        get => Setting?.LocalEndpointVariable ?? string.Empty;
        set { if (Setting == null || Setting.LocalEndpointVariable == value) return; Setting.LocalEndpointVariable = value; OnPropertyChanged(); QueueSave(); }
    }
}
