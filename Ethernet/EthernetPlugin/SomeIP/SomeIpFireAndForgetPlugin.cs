using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpFireAndForgetPlugin : StepPluginBase<SomeIpFireAndForgetSetting>
{
    public override string StepTypeId  => "SomeIp.FireAndForget";
    public override string DisplayName => "SomeIp_FireAndForget";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/someip_fire_and_forget.png";

    public override string Description => """
        ## 功能

        发送 SOME/IP 无响应方法调用（RequestNoReturn，支持 UDP/TCP）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RemoteHost | string([ExpressionField]) | 是 | "192.168.1.20" | 服务端 IP 或域名，求值结果为 string |
        | RemotePort | string([ExpressionField]) | 是 | "30501" | 服务端端口，求值结果为 string |
        | Transport | 枚举 | 否 | Udp | 传输方式，可选值：Udp, Tcp |
        | SendTimeoutMs | int | 否 | 3000 | 地址解析、TCP 连接和提交发送共用总超时（ms），必须大于 0；旧序列缺省为 3000 |
        | ServiceId | string([ExpressionField]) | 是 | "0x1234" | 服务 ID，求值结果为 string |
        | MethodId | string([ExpressionField]) | 是 | "0x0001" | 方法 ID，求值结果为 string |
        | ClientId | string([ExpressionField]) | 否 | "0x0001" | 客户端 ID，求值结果为 string |
        | InterfaceVersion | string([ExpressionField]) | 否 | "0x01" | 接口版本，求值结果为 string |
        | Payload | string([ExpressionField]) | 否 | 空 | 十六进制负载，可为空，求值结果为 string |

        ## 行为

        - 本次 UDP/TCP 临时连接登记为执行资源；中断或退出调试时请求取消，后台确认原始 I/O 与取消回调退出后才释放；旧资源收尾等待与建立计入原 SendTimeoutMs 总预算，已提交的报文不能据此视为撤回

        - 本机发送调用成功即返回 Completed，不等待响应，也不证明远端已收到或执行
        - UDP 域名使用可取消的异步解析；解析、连接和发送共用 SendTimeoutMs，不在阶段切换时重新计时
        - 超时返回 Error、原调用方取消返回 Aborted；已经开始提交时远端执行结果未知，不自动重发。等待真实网络调用退出后才释放本次 Socket

        ## 相关插件

        - `SomeIp_Request`：带响应的 RPC 请求
        """;

    public override IStepExecutor CreateExecutor() => new SomeIpFireAndForgetExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP FireAndForget: {s.RemoteHost}:{s.RemotePort} Service={s.ServiceId} Method={s.MethodId}, 总超时={s.SendTimeoutMs} ms";
    }
}
