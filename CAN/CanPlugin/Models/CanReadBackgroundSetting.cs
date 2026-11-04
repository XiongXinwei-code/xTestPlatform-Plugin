using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanReceiveFrameFormat { Any = 0, Classic = 1, Fd = 2 }

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class CanReceiveMapping : INotifyPropertyChanged
{
    private bool _enabled = true;
    private string _canId = "\"0x123\"", _dataVariable = "";
    private CanFrameType _frameType = CanFrameType.Standard;
    private CanReceiveFrameFormat _frameFormat = CanReceiveFrameFormat.Any;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    [ExpressionField] public string CanId { get => _canId; set => Set(ref _canId, value); }
    public CanFrameType FrameType { get => _frameType; set => Set(ref _frameType, value); }
    public CanReceiveFrameFormat FrameFormat { get => _frameFormat; set => Set(ref _frameFormat, value); }
    [VariablePathField] public string DataVariable { get => _dataVariable; set => Set(ref _dataVariable, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T storage, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value; PropertyChanged?.Invoke(this, new(name));
    }
    public override string ToString() => JsonSerializer.Serialize(this);
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class CanReadBackgroundSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"CAN1\"";
    [ExpressionField] public string TaskName { get; set; } = "\"CanRx1\"";
    public int TimeoutMs { get; set; } = 5000;
    public int PublishIntervalMs { get; set; } = 20;
    public int MaxAgeMs { get; set; } = 1000;
    [VariablePathField] public string ResultVariable { get; set; } = "";
    public ObservableCollection<CanReceiveMapping> Mappings { get; set; } = [];
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class CanReadBackgroundStopSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"CAN1\"";
    [ExpressionField] public string TaskName { get; set; } = "\"CanRx1\"";
    public int TimeoutMs { get; set; } = 5000;
    public bool IgnoreIfMissing { get; set; }
}
