using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 写入插件，向仪器发送 SCPI 命令（不读取响应）
/// </summary>
public sealed class VisaWritePlugin : StepPluginBase<VisaWriteSetting>
{
    public override string StepTypeId => "IO.VisaWrite";
    public override string DisplayName => "VISA_Write";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description =>
        "向 VISA 仪器发送 SCPI 命令，不等待响应。" +
        "Setting 字段：ConnectionName(string,表达式), Command(string,表达式,SCPI命令)。";

    public override IStepExecutor CreateExecutor() => new VisaWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.ConnectionName}: {s.Command}";
    }
}
