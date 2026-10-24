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
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/uds_read_dtc.png";

    public override string Description => """
        ## 功能

        读取 ECU 故障码（UDS 服务 0x19），结果以十六进制字符串存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SubFunction | byte | 否 | 0x02 | 子功能，如 0x02=报告 DTC 及状态 |
        | StatusMask | byte | 否 | 0xFF | DTC 状态掩码 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量名，写入类型为 string（十六进制 DTC 数据）；如 Locals.result |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名，求值结果为 string |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID，如 0x7DF，求值结果为 string |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID，如 0x7E8，求值结果为 string |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |
        | TransactionTimeoutMs | int | 否 | 60000 | 单个 UDS 事务总期限，包含排队、发送、响应及 NRC 0x78；必须大于 0，不是整个 Flash 步骤期限 |
        | MaxResponseBytes | int | 否 | 4194304 | ISO-TP 响应重组上限（字节），必须大于 0；编辑器连接区可配置，超限在分配前拒绝；不是下载块大小 |

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
