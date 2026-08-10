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
        "批量发送多条 SCPI 命令到 VISA 仪器，按顺序逐条发送，每条命令发送后可指定延时等待。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的VISA连接标识名), Items(集合,命令列表,每个元素结构见下方JSON示例)。" +
        "Items 元素JSON示例: {\"Command\":\"*RST\",\"DelayMs\":100} " +
        "Command(string,表达式,SCPI命令), DelayMs(int,发送后延时毫秒,0=不延时)。";

    public override IStepExecutor CreateExecutor() => new VisaBatchWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.ConnectionName}: {s.Items.Count} 条命令";
    }
}
