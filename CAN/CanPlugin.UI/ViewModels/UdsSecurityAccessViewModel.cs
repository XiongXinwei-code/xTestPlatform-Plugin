using CAN.UDS.Models;

namespace CAN.UI.ViewModels;

public class UdsSecurityAccessViewModel : UdsViewModelBase<UdsSecurityAccessSetting>
{
    public override string this[string name] => name == nameof(SecurityLevel) && SecurityLevel is < 1 or > 63
        ? "安全等级序号必须为 1~63；Seed=2×等级−1，Key=2×等级" : base[name];
    public int SecurityLevel { get => Setting?.SecurityLevel ?? 1; set { if (Setting == null || Setting.SecurityLevel == value) return; Setting.SecurityLevel = value; OnPropertyChanged(); QueueSave(); } }
    public string SeedVariable { get => Setting?.SeedVariable ?? "Seed"; set { if (Setting == null || Setting.SeedVariable == value) return; Setting.SeedVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string KeyExpression { get => Setting?.KeyExpression ?? ""; set { if (Setting == null || Setting.KeyExpression == value) return; Setting.KeyExpression = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}
