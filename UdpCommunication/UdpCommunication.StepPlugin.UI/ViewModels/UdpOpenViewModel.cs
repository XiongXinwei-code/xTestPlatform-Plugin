using UdpCommunication.Models;

namespace UdpCommunication.UI.ViewModels;

public sealed class UdpOpenViewModel : UdpViewModelBase
{
    private UdpOpenSetting? _setting;

    protected override void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (UdpOpenSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (UdpOpenSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    protected override object? GetSetting() => _setting;

    protected override void OnSelectedOpenStepChanged(UdpOpenOption? option)
    {
        // UDP_Open 步骤本身是连接的创建者，不引用其它 Open 步骤，此处无需处理。
    }

    public string LocalAddress
    {
        get => _setting?.LocalAddress ?? "\"0.0.0.0\"";
        set { if (_setting != null && _setting.LocalAddress != value) { _setting.LocalAddress = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int LocalPort
    {
        get => _setting?.LocalPort ?? 0;
        set { if (_setting != null && _setting.LocalPort != value) { _setting.LocalPort = value; OnPropertyChanged(); QueueSave(); } }
    }

    public string DefaultRemoteAddress
    {
        get => _setting?.DefaultRemoteAddress ?? "\"127.0.0.1\"";
        set { if (_setting != null && _setting.DefaultRemoteAddress != value) { _setting.DefaultRemoteAddress = value; OnPropertyChanged(); QueueSave(); } }
    }

    public int DefaultRemotePort
    {
        get => _setting?.DefaultRemotePort ?? 5000;
        set { if (_setting != null && _setting.DefaultRemotePort != value) { _setting.DefaultRemotePort = value; OnPropertyChanged(); QueueSave(); } }
    }
}
