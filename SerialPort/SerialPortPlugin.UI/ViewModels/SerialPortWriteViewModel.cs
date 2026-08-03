using SerialPort.Models;

namespace SerialPort.UI.ViewModels;

public sealed class SerialPortWriteViewModel : SerialPortViewModelBase
{
    private SerialPortWriteSetting? _setting;

    protected override void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (SerialPortWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (SerialPortWriteSetting)_serializer.CreateDefault();
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

    public string WriteData
    {
        get => _setting?.WriteData ?? string.Empty;
        set { if (_setting != null && _setting.WriteData != value) { _setting.WriteData = value; OnPropertyChanged(); QueueSave(); } }
    }

    public SerialPortDataFormat DataFormat
    {
        get => _setting?.DataFormat ?? SerialPortDataFormat.String;
        set { if (_setting != null && _setting.DataFormat != value) { _setting.DataFormat = value; OnPropertyChanged(); QueueSave(); } }
    }
}
