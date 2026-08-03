using SerialPort.Models;

namespace SerialPort.UI.ViewModels;

public sealed class SerialPortOpenViewModel : SerialPortViewModelBase
{
    private SerialPortOpenSetting? _setting;

    protected override void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (SerialPortOpenSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (SerialPortOpenSetting)_serializer.CreateDefault();
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

    public int BaudRate
    {
        get => _setting?.BaudRate ?? 9600;
        set { if (_setting != null && _setting.BaudRate != value) { _setting.BaudRate = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int DataBits
    {
        get => _setting?.DataBits ?? 8;
        set { if (_setting != null && _setting.DataBits != value) { _setting.DataBits = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int StopBits
    {
        get => _setting?.StopBits ?? 1;
        set { if (_setting != null && _setting.StopBits != value) { _setting.StopBits = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int Parity
    {
        get => _setting?.Parity ?? 0;
        set { if (_setting != null && _setting.Parity != value) { _setting.Parity = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int ReadTimeoutMs
    {
        get => _setting?.ReadTimeoutMs ?? 3000;
        set { if (_setting != null && _setting.ReadTimeoutMs != value) { _setting.ReadTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int WriteTimeoutMs
    {
        get => _setting?.WriteTimeoutMs ?? 3000;
        set { if (_setting != null && _setting.WriteTimeoutMs != value) { _setting.WriteTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    }
}
