using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsRoutineControlViewModel : UdsViewModelBase<UdsRoutineControlSetting>
{
    private static readonly RoutineControlType[] ControlTypes = [RoutineControlType.Start, RoutineControlType.Stop, RoutineControlType.RequestResults];

    public int ControlTypeIndex { get => Setting == null ? 0 : Array.IndexOf(ControlTypes, Setting.ControlType); set { if (Setting == null || value < 0 || value >= ControlTypes.Length) return; Setting.ControlType = ControlTypes[value]; OnPropertyChanged(); QueueSave(); } }
    public string RoutineId { get => Setting?.RoutineId ?? ""; set { if (Setting == null || Setting.RoutineId == value) return; Setting.RoutineId = value; OnPropertyChanged(); QueueSave(); } }
    public string OptionRecord { get => Setting?.OptionRecord ?? ""; set { if (Setting == null || Setting.OptionRecord == value) return; Setting.OptionRecord = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}
