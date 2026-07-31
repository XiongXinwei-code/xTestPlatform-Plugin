using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 批量写入插件，按顺序发送多条 SCPI 命令，支持命令间延时
/// </summary>
public sealed class VisaBatchWritePlugin : StepPluginBase<VisaBatchWriteSetting>
{
    public override string StepTypeId => "IO.VisaBatchWrite";
    public override string DisplayName => "VISA_BatchWrite";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description =>
        "批量发送多条 SCPI 命令到 VISA 仪器，支持每条命令间延时。" +
        "Setting 字段：ConnectionName(string,表达式), Items(命令列表,每项含Command和DelayMs)。";

    public override IStepExecutor CreateExecutor() => new VisaBatchWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.ConnectionName}: {s.Items.Count} 条命令";
    }
}
