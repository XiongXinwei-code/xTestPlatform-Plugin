using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public class UdpReceiveViewModel : EthernetViewModelBase<UdpReceiveSetting>
{
    private static readonly EthernetDataEncoding[] Encodings =
        [EthernetDataEncoding.Hex, EthernetDataEncoding.Utf8, EthernetDataEncoding.Ascii];

    private static readonly UdpBindMode[] BindModes =
        [UdpBindMode.AnyInterface, UdpBindMode.LocalPort];

    public int LocalPort
    {
        get => Setting?.LocalPort ?? 30490;
        set { if (Setting == null || Setting.LocalPort == value) return; Setting.LocalPort = value; OnPropertyChanged(); QueueSave(); }
    }
    public int BindModeIndex
    {
        get => Setting == null ? 0 : Array.IndexOf(BindModes, Setting.BindMode);
        set { if (Setting == null || value < 0 || value >= BindModes.Length) return; Setting.BindMode = BindModes[value]; OnPropertyChanged(); QueueSave(); }
    }
    public int ExpectedLength
    {
        get => Setting?.ExpectedLength ?? 0;
        set { if (Setting == null || Setting.ExpectedLength == value) return; Setting.ExpectedLength = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 3000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public int EncodingIndex
    {
        get => Setting == null ? 0 : Array.IndexOf(Encodings, Setting.Encoding);
        set { if (Setting == null || value < 0 || value >= Encodings.Length) return; Setting.Encoding = Encodings[value]; OnPropertyChanged(); QueueSave(); }
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
