using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP;

public sealed class XcpDisconnectPlugin : StepPluginBase<XcpDisconnectSetting>
{
    public override string StepTypeId  => "XCP.Disconnect";
    public override string DisplayName => "XCP_Disconnect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        断开 XCP on CAN 连接，向从站发送 DISCONNECT 命令。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | "0x7E1" | XCP 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | "0x7E9" | XCP 响应 CAN ID |
        | TimeoutMs | int | 否 | 1000 | 响应超时毫秒数 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 断开后 XCP 会话结束，CAN 通道仍保持打开

        ## 相关插件

        - `XCP_Connect`：建立 XCP 连接
        """;

    public override IStepExecutor CreateExecutor() => new XcpDisconnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP Disconnect TX={s.TxId} RX={s.RxId}";
    }
}
