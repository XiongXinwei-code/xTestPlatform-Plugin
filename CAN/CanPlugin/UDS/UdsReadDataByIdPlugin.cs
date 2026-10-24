using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsReadDataByIdPlugin : StepPluginBase<UdsReadDataByIdSetting>
{
    public override string StepTypeId => "UDS.ReadDataByID";
    public override string DisplayName => "UDS_ReadDataByID";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/uds_read_did.png";

    public override string Description => """
        ## 功能

        通过 DID 读取 ECU 数据（UDS 服务 0x22），结果以十六进制字符串存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | Did | string([ExpressionField]) | 是 | — | 数据标识符 0~0xFFFF，如 0xF190，求值结果为 string；越界在发送前拒绝 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量名，写入类型为 string（十六进制响应数据）；如 Locals.result |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名，求值结果为 string |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID，如 0x7DF，求值结果为 string |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID，如 0x7E8，求值结果为 string |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |
        | TransactionTimeoutMs | int | 否 | 60000 | 单个 UDS 事务总期限，包含排队、发送、响应及 NRC 0x78；必须大于 0，不是整个 Flash 步骤期限 |
        | MaxResponseBytes | int | 否 | 4194304 | ISO-TP 响应重组上限（字节），必须大于 0；编辑器连接区可配置，超限在分配前拒绝；不是下载块大小 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `UDS_WriteDataByID`：通过 DID 写入数据
        """;

    public override IStepExecutor CreateExecutor() => new UdsReadDataByIdExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"ReadDataByID DID={s.Did} → {s.ResultVariable}";
    }
}
