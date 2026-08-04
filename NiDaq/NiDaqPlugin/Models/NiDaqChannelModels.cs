using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqAiChannel : INotifyPropertyChanged
{
    private string _physicalChannel = string.Empty;
    private string _columnName = string.Empty;
    private double _minValue = -10.0;
    private double _maxValue = 10.0;
    private AiTerminalConfig _terminal = AiTerminalConfig.Differential;

    /// <summary>物理通道，如 "Dev1/ai0"</summary>
    public string PhysicalChannel { get => _physicalChannel; set => SetProperty(ref _physicalChannel, value); }

    /// <summary>列名标识</summary>
    public string ColumnName { get => _columnName; set => SetProperty(ref _columnName, value); }

    /// <summary>量程下限</summary>
    public double MinValue { get => _minValue; set => SetProperty(ref _minValue, value); }

    /// <summary>量程上限</summary>
    public double MaxValue { get => _maxValue; set => SetProperty(ref _maxValue, value); }

    /// <summary>终端配置</summary>
    public AiTerminalConfig Terminal { get => _terminal; set => SetProperty(ref _terminal, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

[MessagePackObject(true)]
public class NiDaqSyncEncoderChannel : INotifyPropertyChanged
{
    private string _counterChannel = string.Empty;
    private string _columnName = string.Empty;
    private EncoderDecodingType _decodingType = EncoderDecodingType.X4;
    private int _pulsesPerRevolution = 1024;
    private double _distancePerPulse = 0.3515625;
    private EncoderUnit _unit = EncoderUnit.Degrees;
    private bool _zIndexEnable = false;

    /// <summary>Counter 通道，如 "Dev1/ctr0"</summary>
    public string CounterChannel { get => _counterChannel; set => SetProperty(ref _counterChannel, value); }

    /// <summary>列名标识</summary>
    public string ColumnName { get => _columnName; set => SetProperty(ref _columnName, value); }

    /// <summary>解码类型</summary>
    public EncoderDecodingType DecodingType { get => _decodingType; set => SetProperty(ref _decodingType, value); }

    /// <summary>每转脉冲数</summary>
    public int PulsesPerRevolution { get => _pulsesPerRevolution; set => SetProperty(ref _pulsesPerRevolution, value); }

    /// <summary>每脉冲距离/角度</summary>
    public double DistancePerPulse { get => _distancePerPulse; set => SetProperty(ref _distancePerPulse, value); }

    /// <summary>输出单位</summary>
    public EncoderUnit Unit { get => _unit; set => SetProperty(ref _unit, value); }

    /// <summary>是否启用 Z 索引</summary>
    public bool ZIndexEnable { get => _zIndexEnable; set => SetProperty(ref _zIndexEnable, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
