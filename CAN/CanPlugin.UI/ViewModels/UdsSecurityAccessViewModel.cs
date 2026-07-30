using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsSecurityAccessViewModel : UdsViewModelBase<UdsSecurityAccessSetting>
{
    public int SecurityLevel { get => Setting?.SecurityLevel ?? 1; set { if (Setting == null || Setting.SecurityLevel == value) return; Setting.SecurityLevel = value; OnPropertyChanged(); QueueSave(); } }
    public string KeyExpression { get => Setting?.KeyExpression ?? ""; set { if (Setting == null || Setting.KeyExpression == value) return; Setting.KeyExpression = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}
