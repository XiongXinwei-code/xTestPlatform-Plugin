using MessagePack;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
public class CyclicMessageItem : INotifyPropertyChanged
{
    private string _canId = "\"0x7FF\"";
    private CanFrameType _frameType = CanFrameType.Standard;
    private string _data = "\"FF FF FF FF FF FF FF FF\"";
    private int _cycleTimeMs = 100;
    private bool _enabled = true;

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

    /// <summary>帧类型（整数形式，供 UI 绑定）</summary>
    [IgnoreMember]
    public int FrameTypeInt
    {
        get => (int)_frameType;
        set { if ((int)_frameType != value) { _frameType = (CanFrameType)value; OnPropertyChanged(); OnPropertyChanged(nameof(FrameType)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
