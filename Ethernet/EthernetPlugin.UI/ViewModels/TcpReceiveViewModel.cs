using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public class TcpReceiveViewModel : EthernetViewModelBase<TcpReceiveSetting>
{
    public bool UseDelimiter
    {
        get => Setting?.UseDelimiter ?? false;
        set { if (Setting == null || Setting.UseDelimiter == value) return; Setting.UseDelimiter = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLengthMode)); QueueSave(); }
    }
    public bool IsLengthMode => !UseDelimiter;
    public string DelimiterHex
    {
        get => Setting?.DelimiterHex ?? "\"0D 0A\"";
        set { if (Setting == null || Setting.DelimiterHex == value) return; Setting.DelimiterHex = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool IncludeDelimiter
    {
        get => Setting?.IncludeDelimiter ?? false;
        set { if (Setting == null || Setting.IncludeDelimiter == value) return; Setting.IncludeDelimiter = value; OnPropertyChanged(); QueueSave(); }
    }
    public int MaxBytes
    {
        get => Setting?.MaxBytes ?? 1048576;
        set { if (Setting == null || Setting.MaxBytes == value) return; Setting.MaxBytes = value; OnPropertyChanged(); QueueSave(); }
    }

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
}
