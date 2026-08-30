using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsReadDtcPlugin : StepPluginBase<UdsReadDtcSetting>
{
    public override string StepTypeId => "UDS.ReadDTC";
    public override string DisplayName => "UDS_ReadDTC";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        读取 ECU 故障码（UDS 服务 0x19），结果以十六进制字符串存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SubFunction | byte | 否 | 0x02 | 子功能，如 0x02=报告 DTC 及状态 |
        | StatusMask | byte | 否 | 0xFF | DTC 状态掩码 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string（十六进制 DTC 数据） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `UDS_ClearDTC`：清除故障码
        """;

    public override IStepExecutor CreateExecutor() => new UdsReadDtcExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"ReadDTC SubFunc=0x{s.SubFunction:X2} Mask=0x{s.StatusMask:X2} → {s.ResultVariable}";
    }
}
