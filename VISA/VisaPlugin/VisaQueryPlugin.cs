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

    public override string Description => """
        ## 功能

        向 VISA 仪器发送查询命令并立即读取响应（Write+Read 一体操作），结果以字符串形式存入指定变量。适用于查询类命令如 *IDN?、:MEAS:VOLT:DC? 等。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 VISA 连接标识名 |
        | Command | string([ExpressionField]) | 是 | — | SCPI 查询命令，如 *IDN? |
        | ResultVariable | string(变量路径) | 是 | — | 结果变量名，写入类型为 string（仪器响应字符串） |
        | TrimResponse | bool | 否 | true | 是否去除响应首尾空白 |

        ## 行为

        - 先写入命令，再立即读取一次响应
        - 连接不存在或读取超时时步骤报错

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        - `VISA_Write` / `VISA_Read`：单独的写入/读取操作
        """;

    public override IStepExecutor CreateExecutor() => new VisaQueryExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Query {s.ConnectionName}: {s.Command} => {s.ResultVariable}";
    }
}
