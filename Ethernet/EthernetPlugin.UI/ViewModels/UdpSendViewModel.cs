using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public class UdpSendViewModel : EthernetViewModelBase<UdpSendSetting>
{
    public bool UseConnection
    {
        get => Setting?.UseConnection ?? false;
        set { if (Setting == null || Setting.UseConnection == value) return; Setting.UseConnection = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTransient)); QueueSave(); }
    }
    public bool IsTransient => !UseConnection;
    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? "\"UDP1\"";
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableBroadcast
    {
        get => Setting?.EnableBroadcast ?? false;
        set { if (Setting == null || Setting.EnableBroadcast == value) return; Setting.EnableBroadcast = value; OnPropertyChanged(); QueueSave(); }
    }

    private static readonly EthernetDataEncoding[] Encodings =
        [EthernetDataEncoding.Hex, EthernetDataEncoding.Utf8, EthernetDataEncoding.Ascii];

    public string RemoteHost
    {
        get => Setting?.RemoteHost ?? "\"192.168.1.255\"";
        set { if (Setting == null || Setting.RemoteHost == value) return; Setting.RemoteHost = value; OnPropertyChanged(); QueueSave(); }
    }
    public string RemotePort
    {
        get => Setting?.RemotePort ?? "\"30490\"";
        set { if (Setting == null || Setting.RemotePort == value) return; Setting.RemotePort = value; OnPropertyChanged(); QueueSave(); }
    }
    public int LocalPort
    {
        get => Setting?.LocalPort ?? 0;
        set { if (Setting == null || Setting.LocalPort == value) return; Setting.LocalPort = value; OnPropertyChanged(); QueueSave(); }
    }
    public int SendTimeoutMs
    {
        get => Setting?.SendTimeoutMs ?? 3000;
        set { if (Setting == null || Setting.SendTimeoutMs == value) return; Setting.SendTimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public string Data
    {
        get => Setting?.Data ?? "\"01 02 03\"";
        set { if (Setting == null || Setting.Data == value) return; Setting.Data = value; OnPropertyChanged(); QueueSave(); }
    }
    public int EncodingIndex
    {
        get => Setting == null ? 0 : Array.IndexOf(Encodings, Setting.Encoding);
        set { if (Setting == null || value < 0 || value >= Encodings.Length) return; Setting.Encoding = Encodings[value]; OnPropertyChanged(); QueueSave(); }
    }
}
