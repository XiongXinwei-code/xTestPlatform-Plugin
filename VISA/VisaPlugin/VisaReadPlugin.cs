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

    public override string Description => """
        ## 功能

        从 VISA 仪器读取响应数据（用于之前 Write 后延迟读取的场景），结果以字符串形式存入指定变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 VISA 连接标识名 |
        | ResultVariable | string | 是 | — | 结果变量名，写入类型为 string（仪器响应字符串） |
        | TrimResponse | bool | 否 | true | 是否去除响应首尾空白 |

        ## 行为

        - 连接不存在或读取超时时步骤报错

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        - `VISA_Write`：发送命令
        - `VISA_Query`：写+读一体操作
        """;

    public override IStepExecutor CreateExecutor() => new VisaReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read {s.ConnectionName} => {s.ResultVariable}";
    }
}
