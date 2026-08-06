using CAN.XCP;
using CAN.XCP.Models;

namespace CAN.UI.ViewModels;

public class XcpConnectViewModel : XcpViewModelBase<XcpConnectSetting>
{
    private static readonly XcpConnectMode[] Modes = [XcpConnectMode.Normal, XcpConnectMode.UserDefined];

    public int ConnectModeIndex
    {
        get => Setting == null ? 0 : Array.IndexOf(Modes, Setting.ConnectMode);
        set { if (Setting == null || value < 0 || value >= Modes.Length) return; Setting.ConnectMode = Modes[value]; OnPropertyChanged(); QueueSave(); }
    }

    public string ResourceVariable
    {
        get => Setting?.ResourceVariable ?? string.Empty;
        set { if (Setting == null || Setting.ResourceVariable == value) return; Setting.ResourceVariable = value; OnPropertyChanged(); QueueSave(); }
    }
}
