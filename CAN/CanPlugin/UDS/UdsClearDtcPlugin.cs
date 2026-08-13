using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsClearDtcPlugin : StepPluginBase<UdsClearDtcSetting>
{
    public override string StepTypeId => "UDS.ClearDTC";
    public override string DisplayName => "UDS_ClearDTC";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        清除 ECU 故障码（UDS 服务 0x14）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | DtcGroup | string([ExpressionField]) | 否 | 0xFFFFFF | DTC 组，0xFFFFFF 表示全部清除 |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `UDS_ReadDTC`：读取故障码
        """;

    public override IStepExecutor CreateExecutor() => new UdsClearDtcExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"ClearDTC Group={s.DtcGroup}";
    }
}
