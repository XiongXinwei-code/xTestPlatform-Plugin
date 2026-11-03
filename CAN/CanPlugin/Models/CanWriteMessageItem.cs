using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class CanWriteMessageItem : INotifyPropertyChanged
{
    private bool _enabled = true, _useFdFrame;
    private string _canId = "\"0x7DF\"", _data = "\"\"";
    private CanFrameType _frameType;
    private CanPayloadProtectionSetting _protection = new();
    public CanWriteMessageItem() => _protection.PropertyChanged += ProtectionChanged;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    [ExpressionField] public string CanId { get => _canId; set => Set(ref _canId, value); }
    [ExpressionField] public string Data { get => _data; set => Set(ref _data, value); }
    public CanFrameType FrameType { get => _frameType; set => Set(ref _frameType, value); }
    public bool UseFdFrame { get => _useFdFrame; set => Set(ref _useFdFrame, value); }
    public CanPayloadProtectionSetting Protection
    {
        get => _protection;
        set { _protection.PropertyChanged -= ProtectionChanged; _protection = value ?? new(); _protection.PropertyChanged += ProtectionChanged; Changed(); }
    }
    private void ProtectionChanged(object? sender, PropertyChangedEventArgs e) => Changed(nameof(Protection));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Changed(name); return true; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
