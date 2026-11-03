using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpOpenPlugin : Ethernet.PluginInfrastructure.StrictStepPluginBase<UdpOpenSetting>
{
    public override string StepTypeId => "Ethernet.UdpOpen";
    public override string DisplayName => "Ethernet_UDP_Connection_Open";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/tcp_open.png";
    public override string Description => """
        ## 功能

        绑定并注册命名 UDP socket，供独立收发步骤复用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "UDP1" | UDP 连接名；与 TCP 的同名资源互不影响 |
        | TimeoutMs | int | 否 | 3000 | 操作总期限，必须大于 0 |
        | LocalAddress | string([ExpressionField]) | 是 | "0.0.0.0" | 本机 IPv4/IPv6 地址，0.0.0.0 监听所有 IPv4 网卡 |
        | LocalPort | int | 否 | 30490 | 0~65535，0 由系统分配端口 |
        | EnableBroadcast | bool | 否 | false | 显式允许 UDP 广播；广播可达性由网卡与路由决定 |
        | LocalEndpointVariable | string(VariablePathField) | 否 | 空 | 可选已声明 string 变量，输出绑定端点，例如 127.0.0.1:30490 |

        ## 行为

        - Open 仅绑定本地 socket，不执行 TCP 握手，也不证明远端设备在线。
        - 后续 UDP Send/Receive 勾选 UseConnection 并使用相同 ConnectionName；接收缓冲跨步骤保留，同一源端口可先发送后收回复。
        - 同名重新打开先关闭旧代次；UDP Close 与执行资源清理负责释放端口。关闭或替换后旧操作不得发布结果。

        - 原调用方取消返回 Aborted；内部超时/失败返回 Error，正常完成返回 Completed。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Ethernet", () => new UdpOpenExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP Open: {s.ConnectionName} ({s.TimeoutMs} ms)";
    }
}
