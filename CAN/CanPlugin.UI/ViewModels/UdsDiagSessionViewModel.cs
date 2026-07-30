using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsDiagSessionViewModel : UdsViewModelBase<UdsDiagSessionSetting>
{
    private static readonly DiagSessionType[] SessionTypes = [DiagSessionType.Default, DiagSessionType.Programming, DiagSessionType.Extended];

    public int SessionTypeIndex { get => Setting == null ? 2 : Array.IndexOf(SessionTypes, Setting.SessionType); set { if (Setting == null || value < 0 || value >= SessionTypes.Length) return; Setting.SessionType = SessionTypes[value]; OnPropertyChanged(); QueueSave(); } }
    public bool SuppressPositiveResponse { get => Setting?.SuppressPositiveResponse ?? false; set { if (Setting == null || Setting.SuppressPositiveResponse == value) return; Setting.SuppressPositiveResponse = value; OnPropertyChanged(); QueueSave(); } }
}
