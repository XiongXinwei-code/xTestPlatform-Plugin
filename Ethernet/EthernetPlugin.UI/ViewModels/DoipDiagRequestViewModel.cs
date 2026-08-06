using Ethernet.DoIP.Models;

namespace Ethernet.UI.ViewModels;

public class DoipDiagRequestViewModel : EthernetViewModelBase<DoipDiagRequestSetting>
{
    public string SessionName
    {
        get => Setting?.SessionName ?? "\"DOIP1\"";
        set { if (Setting == null || Setting.SessionName == value) return; Setting.SessionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public string TargetAddress
    {
        get => Setting?.TargetAddress ?? "\"0x1000\"";
        set { if (Setting == null || Setting.TargetAddress == value) return; Setting.TargetAddress = value; OnPropertyChanged(); QueueSave(); }
    }
    public string RequestData
    {
        get => Setting?.RequestData ?? "\"22 F1 90\"";
        set { if (Setting == null || Setting.RequestData == value) return; Setting.RequestData = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 3000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ResultVariable
    {
        get => Setting?.ResultVariable ?? string.Empty;
        set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableLog
    {
        get => Setting?.EnableLog ?? true;
        set { if (Setting == null || Setting.EnableLog == value) return; Setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }
}
