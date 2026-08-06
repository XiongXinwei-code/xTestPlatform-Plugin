using MessagePack;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinCyclicFrameItem : INotifyPropertyChanged
{
    private string _frameId = "0";
    private string _data = "\"FF FF FF FF FF FF FF FF\"";
    private int _cycleTimeMs = 100;
    private LinChecksumType _checksumType = LinChecksumType.Enhanced;
    private bool _enabled = true;

    /// <summary>LIN 帧 ID（0-63，支持表达式）</summary>
    [ExpressionField]
    public string FrameId
    {
        get => _frameId;
        set => SetProperty(ref _frameId, value);
    }

    /// <summary>发送数据（十六进制字符串，支持表达式）</summary>
    [ExpressionField]
    public string Data
    {
        get => _data;
        set => SetProperty(ref _data, value);
    }

    /// <summary>周期时间（毫秒）</summary>
    public int CycleTimeMs
    {
        get => _cycleTimeMs;
        set => SetProperty(ref _cycleTimeMs, value);
    }

    /// <summary>校验类型</summary>
    public LinChecksumType ChecksumType
    {
        get => _checksumType;
        set => SetProperty(ref _checksumType, value);
    }

    /// <summary>是否启用此帧</summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
