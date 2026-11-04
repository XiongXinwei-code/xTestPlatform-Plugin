using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using CAN.Models;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Dbc;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DbcFrameFormat { Auto = 0, Classic = 1, Fd = 2 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DbcValueKind { Physical = 0, Raw = 1 }

/// <summary>编辑时读取的不可变来源快照；运行不访问原文件。Text 为已解码文本。</summary>
[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class DbcDocumentSetting
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public string Encoding { get; set; } = "UTF-8";
    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);
}

[MessagePackObject(true)]
public class DbcSourceSetting
{
    public DbcDocumentSetting Document { get; set; } = new();
    // Literal path, not an expression. A nonempty path is the authoritative source.
    public string DbcFilePath { get; set; } = "";
    public string DbcFileEncoding { get; set; } = "Auto";
    public string CatalogQuery { get; set; } = "";
    public int TimeoutMs { get; set; } = 5000;
}

[MessagePackObject(true)]
public class DbcCanSetting : DbcSourceSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"CAN1\"";
    public uint MessageId { get; set; }
    public CanFrameType FrameType { get; set; } = CanFrameType.Standard;
    public DbcFrameFormat FrameFormat { get; set; } = DbcFrameFormat.Auto;
    public CanPayloadProtectionSetting Protection { get; set; } = new();
    public bool UseMessageList { get; set; } = true;
    public ObservableCollection<DbcMessageItem> Messages { get; set; } = [];
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class DbcMessageItem : INotifyPropertyChanged
{
    private bool _enabled = true;
    private uint _messageId;
    private CanFrameType _frameType;
    private DbcFrameFormat _frameFormat;
    private CanPayloadProtectionSetting _protection = new();
    private ObservableCollection<DbcSignalInput> _signals = [];
    private ObservableCollection<DbcSignalOutput> _signalOutputs = [];
    private bool _useDbcCycleTime = true;
    private int _cycleTimeMs = 100;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public uint MessageId { get => _messageId; set => Set(ref _messageId, value); }
    public CanFrameType FrameType { get => _frameType; set => Set(ref _frameType, value); }
    public DbcFrameFormat FrameFormat { get => _frameFormat; set => Set(ref _frameFormat, value); }
    public CanPayloadProtectionSetting Protection { get => _protection; set => Set(ref _protection, value); }
    public ObservableCollection<DbcSignalInput> Signals { get => _signals; set => Set(ref _signals, value); }
    public ObservableCollection<DbcSignalOutput> SignalOutputs { get => _signalOutputs; set => Set(ref _signalOutputs, value); }
    public bool UseDbcCycleTime { get => _useDbcCycleTime; set => Set(ref _useDbcCycleTime, value); }
    public int CycleTimeMs { get => _cycleTimeMs; set => Set(ref _cycleTimeMs, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);
    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value; PropertyChanged?.Invoke(this, new(property));
    }
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class DbcReadSetting : DbcCanSetting
{
    [VariablePathField] public string ResultVariable { get; set; } = "";
    public ObservableCollection<DbcSignalOutput> SignalOutputs { get; set; } = [];
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class DbcReadBackgroundSetting : DbcReadSetting
{
    [ExpressionField] public string TaskName { get; set; } = "\"DbcRx1\"";
    public int PublishIntervalMs { get; set; } = 20;
    public int MaxAgeMs { get; set; } = 1000;
}

[MessagePackObject(true)]
[JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class DbcReadBackgroundStopSetting : CanReadBackgroundStopSetting
{
    public DbcReadBackgroundStopSetting() => TaskName = "\"DbcRx1\"";
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class DbcWriteSetting : DbcCanSetting
{
    public ObservableCollection<DbcSignalInput> Signals { get; set; } = [];
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class DbcCyclicWriteSetting : DbcWriteSetting
{
    [ExpressionField] public string TaskName { get; set; } = "\"DbcTask1\"";
    public bool UseDbcCycleTime { get; set; } = true;
    public int CycleTimeMs { get; set; } = 100;
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class DbcCaptureSetting : DbcReadSetting
{
    public int DurationMs { get; set; } = 1000;
    public int MaxFrames { get; set; } = 10000;
    public bool AllDbcMessages { get; set; }
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class DbcSignalInput : INotifyPropertyChanged
{
    private string _name = "", _value = "0";
    private DbcValueKind _valueKind;
    private bool _selected = true;
    public string Name { get => _name; set => Set(ref _name, value); }
    [ExpressionField] public string Value { get => _value; set => Set(ref _value, value); }
    public DbcValueKind ValueKind { get => _valueKind; set => Set(ref _valueKind, value); }
    public bool Selected { get => _selected; set => Set(ref _selected, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);
    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value; PropertyChanged?.Invoke(this, new(property));
    }
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class DbcSignalOutput : INotifyPropertyChanged
{
    private string _name = "", _targetVariable = "";
    private DbcValueKind _valueKind;
    private bool _selected = true;
    public string Name { get => _name; set => Set(ref _name, value); }
    [VariablePathField] public string TargetVariable { get => _targetVariable; set => Set(ref _targetVariable, value); }
    public DbcValueKind ValueKind { get => _valueKind; set => Set(ref _valueKind, value); }
    public bool Selected { get => _selected; set => Set(ref _selected, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);
    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value; PropertyChanged?.Invoke(this, new(property));
    }
}
