using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsWriteDataByIdPlugin : StepPluginBase<UdsWriteDataByIdSetting>
{
    public override string StepTypeId => "UDS.WriteDataByID";
    public override string DisplayName => "UDS_WriteDataByID";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        通过 DID 向 ECU 写入数据（UDS 服务 0x2E）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | Did | string([ExpressionField]) | 是 | — | 数据标识符，如 0xF199 |
        | Data | string([ExpressionField]) | 是 | — | 十六进制写入数据，如 "01 02 03" |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错
        - 通常需先切换会话并完成安全访问

        ## 相关插件

        - `UDS_DiagSession`：切换诊断会话
        - `UDS_SecurityAccess`：解锁 ECU
        - `UDS_ReadDataByID`：通过 DID 读取数据
        """;

    public override IStepExecutor CreateExecutor() => new UdsWriteDataByIdExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"WriteDataByID DID={s.Did} Data=[{s.Data}]";
    }
}
