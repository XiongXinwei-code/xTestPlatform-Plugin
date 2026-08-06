using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP;

public sealed class XcpConnectPlugin : StepPluginBase<XcpConnectSetting>
{
    public override string StepTypeId  => "XCP.Connect";
    public override string DisplayName => "XCP_Connect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        建立 XCP on CAN 连接，发送 CONNECT 命令并获取从站能力信息。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | 表达式(string) | 是 | "CAN1" | 已打开的 CAN 连接名 |
        | TxId | 表达式(string) | 是 | "0x7E1" | XCP 请求 CAN ID |
        | RxId | 表达式(string) | 是 | "0x7E9" | XCP 响应 CAN ID |
        | TimeoutMs | int | 否 | 1000 | 响应超时毫秒数 |
        | ConnectMode | 枚举 | 否 | Normal | 可选值：Normal, UserDefined |
        | ResourceVariable | string | 否 | 空 | 存储资源掩码的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 连接成功后可执行 ShortUpload/ShortDownload 等 XCP 操作
        - 从站无响应或返回错误时步骤报错

        ## 相关插件

        - `CAN_Open`：先打开 CAN 通道
        - `XCP_Disconnect`：断开 XCP 连接
        """;

    public override IStepExecutor CreateExecutor() => new XcpConnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP Connect TX={s.TxId} RX={s.RxId} ({s.ConnectMode})";
    }
}
