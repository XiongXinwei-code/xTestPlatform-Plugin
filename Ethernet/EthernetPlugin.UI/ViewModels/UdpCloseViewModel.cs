using Ethernet.Models;

namespace Ethernet.UI.ViewModels;

public sealed class UdpCloseViewModel : EthernetViewModelBase<UdpCloseSetting>
{
    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? "\"UDP1\"";
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 5000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
}
