using MessagePack;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class CyclicMessageItem : INotifyPropertyChanged
{
    private string _canId = "\"0x7FF\"";
    private CanFrameType _frameType = CanFrameType.Standard;
    private string _data = "\"FF FF FF FF FF FF FF FF\"";
    private int _cycleTimeMs = 100;
    private bool _enabled = true;
    private string _cycleTimeMsExpression = "";
    private string _enabledExpression = "";
    private bool _useFdFrame;
    private CanPayloadProtectionSetting _protection = new();

    public CyclicMessageItem() => _protection.PropertyChanged += ProtectionChanged;

    /// <summary>每行独立的 RC 与 CRC，子属性修改也触发编辑器保存。</summary>
    public CanPayloadProtectionSetting Protection
    {
        get => _protection;
        set
        {
            var replacement = value ?? new CanPayloadProtectionSetting();
            if (ReferenceEquals(_protection, replacement)) return;
            _protection.PropertyChanged -= ProtectionChanged;
            _protection = replacement;
            _protection.PropertyChanged += ProtectionChanged;
            OnPropertyChanged();
        }
    }

    private void ProtectionChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Protection));

    /// <summary>CAN ID（支持表达式，如 0x185）</summary>
    [ExpressionField]
    public string CanId
    {
        get => _canId;
        set => SetProperty(ref _canId, value);
    }

    /// <summary>帧类型</summary>
    public CanFrameType FrameType
    {
        get => _frameType;
        set => SetProperty(ref _frameType, value);
    }

    /// <summary>发送数据（十六进制字符串，支持表达式）</summary>
    [ExpressionField]
    public string Data
    {
        get => _data;
        set => SetProperty(ref _data, value);
    }

    /// <summary>以 CAN FD/BRS 帧发送；旧序列默认使用经典帧。</summary>
    public bool UseFdFrame
    {
        get => _useFdFrame;
        set => SetProperty(ref _useFdFrame, value);
    }

    /// <summary>发送周期（毫秒）</summary>
    public int CycleTimeMs
    {
        get => _cycleTimeMs;
        set => SetProperty(ref _cycleTimeMs, value);
    }

    /// <summary>是否启用</summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    /// <summary>可选动态周期表达式，结果为 int 毫秒；空值兼容旧 CycleTimeMs。</summary>
    [ExpressionField]
    public string CycleTimeMsExpression
    {
        get => _cycleTimeMsExpression;
        set => SetProperty(ref _cycleTimeMsExpression, value);
    }

    /// <summary>可选动态启用表达式，结果为 bool；暂停和恢复不重置 Counter。</summary>
    [ExpressionField]
    public string EnabledExpression
    {
        get => _enabledExpression;
        set => SetProperty(ref _enabledExpression, value);
    }

    internal CyclicMessageItem Copy() => new()
    {
        CanId = CanId, FrameType = FrameType, Data = Data, UseFdFrame = UseFdFrame,
        CycleTimeMs = CycleTimeMs, Enabled = Enabled,
        CycleTimeMsExpression = CycleTimeMsExpression, EnabledExpression = EnabledExpression,
        Protection = Protection.Copy()
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
