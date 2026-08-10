using UdpCommunication.Models;
using UdpCommunication.Protocol;

namespace UdpCommunication.UI.ViewModels;

public sealed class UdpSendViewModel : UdpViewModelBase
{
    private UdpSendSetting? _setting;

    protected override void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (UdpSendSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (UdpSendSetting)_serializer.CreateDefault();

            RefreshAvailableOpenSteps(_setting.OpenStepAddress);
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    protected override object? GetSetting() => _setting;

    protected override void OnSelectedOpenStepChanged(UdpOpenOption? option)
    {
        if (_setting == null) return;
        if (_setting.OpenStepAddress == option?.StepAddress) return;
        _setting.OpenStepAddress = option?.StepAddress ?? string.Empty;
        QueueSave();
    }

    public string OpenStepAddress
    {
        get => _setting?.OpenStepAddress ?? string.Empty;
        set
        {
            if (_setting != null && _setting.OpenStepAddress != value)
            {
                _setting.OpenStepAddress = value;
                OnPropertyChanged();
                QueueSave();
            }
        }
    }

    public string RemoteAddress
    {
        get => _setting?.RemoteAddress ?? "\"127.0.0.1\"";
        set { if (_setting != null && _setting.RemoteAddress != value) { _setting.RemoteAddress = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int RemotePort
    {
        get => _setting?.RemotePort ?? 5000;
        set { if (_setting != null && _setting.RemotePort != value) { _setting.RemotePort = value; OnPropertyChanged(); QueueSave(); } }
    }

    public string RequestData
    {
        get => _setting?.RequestData ?? "\"\"";
        set { if (_setting != null && _setting.RequestData != value) { _setting.RequestData = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int RequestFormatIndex
    {
        get => (int)(_setting?.RequestFormat ?? UdpPacketFormat.Utf8Text);
        set
        {
            if (_setting != null && (int)_setting.RequestFormat != value)
            {
                _setting.RequestFormat = (UdpPacketFormat)value;
                OnPropertyChanged();
                QueueSave();
            }
        }
    }
}
