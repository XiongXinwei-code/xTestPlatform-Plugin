using SerialPort.Models;

namespace SerialPort.UI.ViewModels;

public sealed class SerialPortCloseViewModel : SerialPortViewModelBase
{
    private SerialPortCloseSetting? _setting;

    protected override void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (SerialPortCloseSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (SerialPortCloseSetting)_serializer.CreateDefault();
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
}
