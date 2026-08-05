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
        "向 VISA 仪器发送查询命令并立即读取响应（Write+Read 一体操作），结果以字符串形式存入指定变量。适用于查询类命令如 *IDN?、:MEAS:VOLT:DC? 等。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的VISA连接标识名), Command(string,表达式,SCPI查询命令如*IDN?), " +
        "ResultVariable(string,结果变量名,写入类型:string 仪器响应字符串), TrimResponse(bool,是否去除首尾空白,默认true)。";

    public override IStepExecutor CreateExecutor() => new VisaQueryExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Query {s.ConnectionName}: {s.Command} => {s.ResultVariable}";
    }
}
