using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsReadDataByIdViewModel : UdsViewModelBase<UdsReadDataByIdSetting>
{
    public string Did { get => Setting?.Did ?? ""; set { if (Setting == null || Setting.Did == value) return; Setting.Did = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}
