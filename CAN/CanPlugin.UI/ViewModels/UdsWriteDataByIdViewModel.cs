using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsWriteDataByIdViewModel : UdsViewModelBase<UdsWriteDataByIdSetting>
{
    public string Did { get => Setting?.Did ?? ""; set { if (Setting == null || Setting.Did == value) return; Setting.Did = value; OnPropertyChanged(); QueueSave(); } }
    public string Data { get => Setting?.Data ?? ""; set { if (Setting == null || Setting.Data == value) return; Setting.Data = value; OnPropertyChanged(); QueueSave(); } }
}
