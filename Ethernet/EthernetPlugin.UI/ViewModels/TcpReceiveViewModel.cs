using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public class TcpReceiveViewModel : EthernetViewModelBase<TcpReceiveSetting>
{
    private static readonly EthernetDataEncoding[] Encodings =
        [EthernetDataEncoding.Hex, EthernetDataEncoding.Utf8, EthernetDataEncoding.Ascii];

    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? "\"TCP1\"";
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
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
