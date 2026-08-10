using UdpCommunication.Models;
using UdpCommunication.Protocol;

namespace UdpCommunication.UI.ViewModels;

public sealed class UdpReceiveViewModel : UdpViewModelBase
{
    private UdpReceiveSetting? _setting;

    protected override void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (UdpReceiveSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (UdpReceiveSetting)_serializer.CreateDefault();

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

    public int ReceiveTimeoutMs
    {
        get => _setting?.ReceiveTimeoutMs ?? 3000;
        set { if (_setting != null && _setting.ReceiveTimeoutMs != value) { _setting.ReceiveTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int ReplyFormatIndex
    {
        get => (int)(_setting?.ReplyFormat ?? UdpPacketFormat.Utf8Text);
        set
        {
            if (_setting != null && (int)_setting.ReplyFormat != value)
            {
                _setting.ReplyFormat = (UdpPacketFormat)value;
                OnPropertyChanged();
                QueueSave();
            }
        }
    }

    public string ExpectedReply
    {
        get => _setting?.ExpectedReply ?? "\"\"";
        set { if (_setting != null && _setting.ExpectedReply != value) { _setting.ExpectedReply = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int MatchModeIndex
    {
        get => (int)(_setting?.MatchMode ?? UdpReplyMatchMode.Exact);
        set
        {
            if (_setting != null && (int)_setting.MatchMode != value)
            {
                _setting.MatchMode = (UdpReplyMatchMode)value;
                OnPropertyChanged();
                QueueSave();
            }
        }
    }

    public string ResponseVariable
    {
        get => _setting?.ResponseVariable ?? "Step.UdpReply";
        set { if (_setting != null && _setting.ResponseVariable != value) { _setting.ResponseVariable = value; OnPropertyChanged(); QueueSave(); } }
    }
}
