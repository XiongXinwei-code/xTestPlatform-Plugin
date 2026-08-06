using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public class TcpSendViewModel : EthernetViewModelBase<TcpSendSetting>
{
    private static readonly EthernetDataEncoding[] Encodings =
        [EthernetDataEncoding.Hex, EthernetDataEncoding.Utf8, EthernetDataEncoding.Ascii];

    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? "\"TCP1\"";
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
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
    public bool EnableLog
    {
        get => Setting?.EnableLog ?? true;
        set { if (Setting == null || Setting.EnableLog == value) return; Setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }
}
