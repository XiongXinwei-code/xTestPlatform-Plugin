using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 查询插件，发送 SCPI 命令并读取响应，结果存入变量
/// </summary>
public sealed class VisaQueryPlugin : StepPluginBase<VisaQuerySetting>
{
    public override string StepTypeId => "IO.VisaQuery";
    public override string DisplayName => "VISA_Query";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description =>
        "向 VISA 仪器发送查询命令并读取响应，结果存入指定变量。" +
        "Setting 字段：ConnectionName(string,表达式), Command(string,表达式,SCPI命令), " +
        "ResultVariable(string,表达式,结果变量名), TrimResponse(bool,是否去除空白)。";

    public override IStepExecutor CreateExecutor() => new VisaQueryExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Query {s.ConnectionName}: {s.Command} => {s.ResultVariable}";
    }
}
