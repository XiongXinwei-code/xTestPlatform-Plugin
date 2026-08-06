using Ethernet.DoIP.Models;

namespace Ethernet.UI.ViewModels;

public class DoipConnectViewModel : EthernetViewModelBase<DoipConnectSetting>
{
    private static readonly DoipActivationType[] ActivationTypes =
        [DoipActivationType.Default, DoipActivationType.WwhObd, DoipActivationType.CentralSecurity];

    public string SessionName
    {
        get => Setting?.SessionName ?? "\"DOIP1\"";
        set { if (Setting == null || Setting.SessionName == value) return; Setting.SessionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public string RemoteHost
    {
        get => Setting?.RemoteHost ?? "\"192.168.1.10\"";
        set { if (Setting == null || Setting.RemoteHost == value) return; Setting.RemoteHost = value; OnPropertyChanged(); QueueSave(); }
    }
    public string RemotePort
    {
        get => Setting?.RemotePort ?? "\"13400\"";
        set { if (Setting == null || Setting.RemotePort == value) return; Setting.RemotePort = value; OnPropertyChanged(); QueueSave(); }
    }
    public string SourceAddress
    {
        get => Setting?.SourceAddress ?? "\"0x0E00\"";
        set { if (Setting == null || Setting.SourceAddress == value) return; Setting.SourceAddress = value; OnPropertyChanged(); QueueSave(); }
    }
    public int ActivationTypeIndex
    {
        get => Setting == null ? 0 : Array.IndexOf(ActivationTypes, Setting.ActivationType);
        set { if (Setting == null || value < 0 || value >= ActivationTypes.Length) return; Setting.ActivationType = ActivationTypes[value]; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 3000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableLog
    {
        get => Setting?.EnableLog ?? true;
        set { if (Setting == null || Setting.EnableLog == value) return; Setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }
}
