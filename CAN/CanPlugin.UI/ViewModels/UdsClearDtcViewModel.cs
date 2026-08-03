using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsClearDtcViewModel : UdsViewModelBase<UdsClearDtcSetting>
{
    public string DtcGroup { get => Setting?.DtcGroup ?? ""; set { if (Setting == null || Setting.DtcGroup == value) return; Setting.DtcGroup = value; OnPropertyChanged(); QueueSave(); } }
}
