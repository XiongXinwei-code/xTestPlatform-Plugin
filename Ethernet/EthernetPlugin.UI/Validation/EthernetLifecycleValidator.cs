using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.UI.Validation;

internal static class EthernetLifecycleValidator
{
    internal static void CheckPrecedingUdpOpen(StepEditorValidationContext context, string name, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(name) || context.Block == null || context.CurrentStep == null) return;
        int index = context.Block.IndexOf(context.CurrentStep);
        foreach (var step in context.Block.Take(Math.Max(0, index)))
        {
            if (step.StepSetting.StepType != "Ethernet.UdpOpen") continue;
            try
            {
                var setting = (Models.UdpOpenSetting)new UdpOpenPlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
                if (string.Equals(setting.ConnectionName, name, StringComparison.Ordinal)) return;
            }
            catch (Exception) { /* 前置步骤设置由自身校验报告。 */ }
        }
        errors.Add(StepSettingError.Warning("ETH_UDP_LC01", $"当前块之前未找到 {name} 的 Ethernet.UdpOpen；若由 Setup/其他块建立，请确认执行顺序"));
    }

    internal static void CheckPrecedingOpen(StepEditorValidationContext context, string name, bool doip, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(name) || context.Block == null || context.CurrentStep == null) return;
        string expected = doip ? "DoIP.Connect" : "Ethernet.TcpOpen";
        int index = context.Block.IndexOf(context.CurrentStep);
        foreach (var step in context.Block.Take(Math.Max(0, index)))
        {
            if (step.StepSetting.StepType != expected) continue;
            try
            {
                var plugin = doip ? (IStepPlugin)new DoIP.DoipConnectPlugin() : new TcpOpenPlugin();
                var setting = plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
                string candidate = doip ? ((DoIP.Models.DoipConnectSetting)setting).SessionName : ((Models.TcpOpenSetting)setting).ConnectionName;
                if (string.Equals(candidate, name, StringComparison.Ordinal)) return;
            }
            catch (Exception) { /* The preceding step has its own setting validation; do not hide this step's errors. */ }
        }
        errors.Add(StepSettingError.Warning(doip ? "DOIP_LC01" : "ETH_LC01",
            $"当前块中此步骤之前未找到针对标识名 {name} 的 {expected}；若由其他块/子序列建立，请确认运行顺序"));
    }
}
