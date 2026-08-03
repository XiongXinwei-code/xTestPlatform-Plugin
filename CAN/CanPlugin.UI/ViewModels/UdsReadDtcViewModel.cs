using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsReadDtcViewModel : UdsViewModelBase<UdsReadDtcSetting>
{
    public int SubFunction { get => Setting?.SubFunction ?? 0x02; set { if (Setting == null || Setting.SubFunction == (byte)value) return; Setting.SubFunction = (byte)value; OnPropertyChanged(); QueueSave(); } }
    public int StatusMask { get => Setting?.StatusMask ?? 0xFF; set { if (Setting == null || Setting.StatusMask == (byte)value) return; Setting.StatusMask = (byte)value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}
