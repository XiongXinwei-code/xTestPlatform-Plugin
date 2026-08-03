using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 读取插件，从仪器读取响应（不发送命令）
/// </summary>
public sealed class VisaReadPlugin : StepPluginBase<VisaReadSetting>
{
    public override string StepTypeId => "IO.VisaRead";
    public override string DisplayName => "VISA_Read";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description =>
        "从 VISA 仪器读取响应数据（用于之前 Write 后延迟读取的场景）。" +
        "Setting 字段：ConnectionName(string,表达式), ResultVariable(string,表达式,结果变量名), TrimResponse(bool,是否去除空白)。";

    public override IStepExecutor CreateExecutor() => new VisaReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read {s.ConnectionName} => {s.ResultVariable}";
    }
}
