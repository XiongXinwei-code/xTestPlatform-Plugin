using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 关闭会话插件，关闭并释放仪器连接资源
/// </summary>
public sealed class VisaClosePlugin : StepPluginBase<VisaCloseSetting>
{
    public override string StepTypeId => "IO.VisaClose";
    public override string DisplayName => "VISA_Close";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description =>
        "关闭指定的 VISA 仪器会话。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名)。";

    public override IStepExecutor CreateExecutor() => new VisaCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}";
    }
}
