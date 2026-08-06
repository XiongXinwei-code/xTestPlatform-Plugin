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

    public override string Description => """
        ## 功能

        向 VISA 仪器发送 SCPI 命令（只写不读），不等待响应。适用于设置类命令如 *RST、:CONF:VOLT:DC 等。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | 表达式(string) | 是 | — | 已打开的 VISA 连接标识名 |
        | Command | 表达式(string) | 是 | — | SCPI 命令，如 *RST |

        ## 行为

        - 连接不存在或写入超时时步骤报错

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        - `VISA_Query`：查询类命令（写+读）
        - `VISA_BatchWrite`：批量发送多条命令
        """;

    public override IStepExecutor CreateExecutor() => new VisaWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.ConnectionName}: {s.Command}";
    }
}
