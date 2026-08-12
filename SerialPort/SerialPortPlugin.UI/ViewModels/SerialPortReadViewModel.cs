using SerialPort.Models;

namespace SerialPort.UI.ViewModels;

public sealed class SerialPortReadViewModel : SerialPortViewModelBase
{
    private SerialPortReadSetting? _setting;

    protected override void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (SerialPortReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (SerialPortReadSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    protected override object? GetSetting() => _setting;

    public string PortName
    {
        get => _setting?.PortName ?? string.Empty;
        set { if (_setting != null && _setting.PortName != value) { _setting.PortName = value; OnPropertyChanged(); QueueSave(); } }
    }

    public SerialPortDataFormat DataFormat
    {
        get => _setting?.DataFormat ?? SerialPortDataFormat.String;
        set { if (_setting != null && _setting.DataFormat != value) { _setting.DataFormat = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int ReadTimeoutMs
    {
        get => _setting?.ReadTimeoutMs ?? 3000;
        set { if (_setting != null && _setting.ReadTimeoutMs != value) { _setting.ReadTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int ReadBytes
    {
        get => _setting?.ReadBytes ?? 0;
        set { if (_setting != null && _setting.ReadBytes != value) { _setting.ReadBytes = value; OnPropertyChanged(); QueueSave(); } }
    }

    public string Terminator
    {
        get => EscapeTerminator(_setting?.Terminator);
        set { if (_setting != null && Terminator != value) { _setting.Terminator = value; OnPropertyChanged(); QueueSave(); } }
    }

    /// <summary>常用终止符预设（转义文本形式），供下拉选择，也可手动输入自定义值或清空（读到超时为止）</summary>
    public IReadOnlyList<string> TerminatorPresets { get; } = new[] { "\\n", "\\r\\n", "\\r" };

    /// <summary>将真实控制字符转义为可见文本（兼容旧数据），空值保持为空</summary>
    private static string EscapeTerminator(string? terminator)
    {
        if (string.IsNullOrEmpty(terminator)) return string.Empty;
        return terminator.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    public string ResultVariable
    {
        get => _setting?.ResultVariable ?? string.Empty;
        set { if (_setting != null && _setting.ResultVariable != value) { _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
    }
}
