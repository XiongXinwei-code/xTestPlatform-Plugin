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
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        通过 DID 读取 ECU 数据（UDS 服务 0x22），结果以十六进制字符串存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | Did | string([ExpressionField]) | 是 | — | 数据标识符，如 0xF190 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string（十六进制响应数据） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

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
