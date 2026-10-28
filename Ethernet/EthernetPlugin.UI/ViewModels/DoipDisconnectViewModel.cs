using Ethernet.DoIP.Models;

namespace Ethernet.UI.ViewModels;

public class DoipDisconnectViewModel : EthernetViewModelBase<DoipDisconnectSetting>, System.ComponentModel.IDataErrorInfo
{
    public string SessionName
    {
        get => Setting?.SessionName ?? "\"DOIP1\"";
        set { if (Setting == null || Setting.SessionName == value) return; Setting.SessionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 5000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public string Error => this[nameof(TimeoutMs)];
    public string this[string name] => name == nameof(TimeoutMs) && TimeoutMs <= 0 ? "关闭总超时必须大于0 ms" : "";
}
