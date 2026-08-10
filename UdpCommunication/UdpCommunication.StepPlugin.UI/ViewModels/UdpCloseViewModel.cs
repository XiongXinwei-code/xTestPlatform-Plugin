using UdpCommunication.Models;

namespace UdpCommunication.UI.ViewModels;

public sealed class UdpCloseViewModel : UdpViewModelBase
{
    private UdpCloseSetting? _setting;

    protected override void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (UdpCloseSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (UdpCloseSetting)_serializer.CreateDefault();

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
}
