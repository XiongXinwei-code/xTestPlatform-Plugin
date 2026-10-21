using CAN;
using CAN.Models;
using CiA402.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.Validation;

public static class AxisLifecycleValidator
{
    public static void Validate(StepEditorValidationContext context, AxisSetting setting, List<StepSettingError> errors)
    {
        var steps = Preceding(context).ToArray();
        if (setting is BindAxisSetting bind)
        {
            if (bind.Transport != TransportKind.CANopen) return;
            var opened = false;
            foreach (var step in steps)
            {
                try
                {
                    if (step.StepSetting.StepType == "IO.CanOpen" &&
                        ((CanOpenSetting)new CanOpenPlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).ConnectionName == bind.ConnectionName) opened = true;
                    if (step.StepSetting.StepType == "IO.CanClose" &&
                        ((CanCloseSetting)new CanClosePlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).ConnectionName == bind.ConnectionName) opened = false;
                }
                catch (Exception) { }
            }
            if (!opened) errors.Add(StepSettingError.Warning("C402_101", "前置路径未找到匹配且未关闭的 CAN_Open；动态表达式和子序列在运行时再次校验"));
            return;
        }
        BindAxisSetting? precedingBind = null;
        OperationMode? mode = null;
        var enabled = false;
        foreach (var step in steps)
        {
            if (!step.StepSetting.StepType.StartsWith("Motion.CiA402", StringComparison.Ordinal)) continue;
            try
            {
                var plugin = StepCatalog.ForId(step.StepSetting.StepType);
                var serializer = plugin.CreateSerializer();
                var prior = step.StepSetting.Setting is { Length: > 0 } data
                    ? (AxisSetting)serializer.Deserialize(data, step.StepSetting.SettingVersion) : (AxisSetting)serializer.CreateDefault();
                if (prior.AxisName != setting.AxisName) continue;
                switch (prior)
                {
                    case BindAxisSetting b: precedingBind = b; enabled = false; mode = null; break;
                    case UnbindAxisSetting: precedingBind = null; enabled = false; mode = null; break;
                    case SetModeSetting m: mode = m.Mode; break;
                    case EnableSetting: enabled = true; break;
                    case DisableSetting or StopMotionSetting or ResetFaultSetting: enabled = false; break;
                }
            }
            catch (Exception) { }
        }
        if (precedingBind == null) errors.Add(StepSettingError.Warning("C402_102", "前置路径未找到匹配且未解除的 CiA402_BindAxis；动态分支在运行时再次校验"));
        var required = setting switch
        {
            MovePositionSetting => OperationMode.PP, RunVelocitySetting => OperationMode.PV, RunTorqueSetting => OperationMode.PT,
            HomeSetting => OperationMode.HM, WriteCspTargetSetting => OperationMode.CSP, WriteCsvTargetSetting => OperationMode.CSV,
            WriteCstTargetSetting => OperationMode.CST, _ => (OperationMode?)null
        };
        if (required != null && mode != required) errors.Add(StepSettingError.Warning("C402_103", $"未发现匹配的 {required} 模式设置步骤"));
        if (setting is MovePositionSetting or RunVelocitySetting or RunTorqueSetting or HomeSetting && !enabled)
            errors.Add(StepSettingError.Warning("C402_104", "未发现匹配的 CiA402_Enable 前置步骤"));
        if (setting is SetModeSetting && enabled) errors.Add(StepSettingError.Warning("C402_105", "切换模式前应先释放运行使能"));
        if (precedingBind != null && setting is ReadDiagnosticsSetting diagnostic)
        {
            if ((!string.IsNullOrWhiteSpace(diagnostic.HardwareValueVariable) || !string.IsNullOrWhiteSpace(diagnostic.HardwareActiveVariable)) && precedingBind.DiagnosticIndex == 0)
                errors.Add(StepSettingError.Error("C402_106", "绑定轴未配置硬件诊断对象"));
            if (!string.IsNullOrWhiteSpace(diagnostic.HardwareActiveVariable) && precedingBind.DiagnosticMask == 0)
                errors.Add(StepSettingError.Error("C402_107", "绑定轴未配置硬件诊断异常掩码"));
        }
    }

    private static IEnumerable<Step> Preceding(StepEditorValidationContext context)
    {
        var owner = context.SequenceFile?.Sequences.Values.FirstOrDefault(sequence => sequence.StepItems.Values.Any(b => ReferenceEquals(b, context.Block)));
        var all = new List<Step>();
        if (owner != null)
        {
            foreach (var kind in new[] { BlockType.Setup, BlockType.Main, BlockType.Cleanup })
                if (owner.StepItems.TryGetValue(kind, out var block)) all.AddRange(block);
        }
        else all.AddRange(context.Block);
        var index = all.IndexOf(context.CurrentStep);
        return index <= 0 ? [] : all.Take(index);
    }
}
