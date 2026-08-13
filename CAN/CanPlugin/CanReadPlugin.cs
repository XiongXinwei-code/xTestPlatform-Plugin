using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanReadPlugin : StepPluginBase<CanReadSetting>
{
    public override string StepTypeId => "IO.CanRead";
    public override string DisplayName => "CAN_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        从已打开的 CAN 通道接收一帧报文，可按 ID 过滤，结果存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | FilterId | string([ExpressionField]) | 否 | 空 | 过滤 CAN ID，如 0x7E8，为空则接收任意帧 |
        | ReadTimeoutMs | int | 否 | — | 读取超时毫秒数 |
        | ResultVariable | string | 是 | — | 结果变量名，写入类型为 string（十六进制报文数据） |
        | IdVariable | string | 否 | 空 | 接收帧 CAN ID 存入的变量名 |
        | EnableLog | bool | 否 | true | 是否输出读取日志 |

        ## 行为

        - 超时未收到匹配报文时步骤报错

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Write`：发送报文
        """;

    public override IStepExecutor CreateExecutor() => new CanReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var filter = string.IsNullOrWhiteSpace(s.FilterId) ? "Any" : s.FilterId;
        return $"Read {s.ConnectionName} Filter={filter} → {s.ResultVariable}";
    }
}
