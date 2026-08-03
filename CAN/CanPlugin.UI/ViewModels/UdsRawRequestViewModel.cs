using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsRawRequestViewModel : UdsViewModelBase<UdsRawRequestSetting>
{
    public string RequestData { get => Setting?.RequestData ?? ""; set { if (Setting == null || Setting.RequestData == value) return; Setting.RequestData = value; OnPropertyChanged(); QueueSave(); } }
    public bool WaitResponse { get => Setting?.WaitResponse ?? true; set { if (Setting == null || Setting.WaitResponse == value) return; Setting.WaitResponse = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}
