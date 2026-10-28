using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public class TcpCloseViewModel : EthernetViewModelBase<TcpCloseSetting>, System.ComponentModel.IDataErrorInfo
{
    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? "\"TCP1\"";
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 5000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public string Error => this[nameof(TimeoutMs)];
    public string this[string name] => name == nameof(TimeoutMs) && TimeoutMs <= 0 ? "关闭总超时必须大于0 ms" : "";
}
