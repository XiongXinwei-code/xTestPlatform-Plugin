using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsReadDtcViewModel : UdsViewModelBase<UdsReadDtcSetting>
{
    public int SubFunction
    {
        get => Setting?.SubFunction ?? 0x02;
        set
        {
            if (value is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(value), "子功能必须为 0～255 的整数");
            if (Setting == null || Setting.SubFunction == value) return;
            Setting.SubFunction = (byte)value; OnPropertyChanged(); QueueSave();
        }
    }
    public int StatusMask
    {
        get => Setting?.StatusMask ?? 0xFF;
        set
        {
            if (value is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(value), "状态掩码必须为 0～255 的整数");
            if (Setting == null || Setting.StatusMask == value) return;
            Setting.StatusMask = (byte)value; OnPropertyChanged(); QueueSave();
        }
    }
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}
