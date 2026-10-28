using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpRequestPlugin : StepPluginBase<SomeIpRequestSetting>
{
    public override string StepTypeId  => "SomeIp.Request";
    public override string DisplayName => "SomeIp_Request";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/someip_request.png";

    public override string Description => """
        ## 功能

        发送 SOME/IP RPC 请求并等待响应（支持 UDP/TCP）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RemoteHost | string([ExpressionField]) | 是 | "192.168.1.20" | 服务端 IP 或域名，求值结果为 string |
        | RemotePort | string([ExpressionField]) | 是 | "30501" | 服务端端口，求值结果为 string |
        | Transport | 枚举 | 否 | Udp | 传输方式，可选值：Udp, Tcp |
        | ServiceId | string([ExpressionField]) | 是 | "0x1234" | 服务 ID，求值结果为 string |
        | MethodId | string([ExpressionField]) | 是 | "0x0001" | 方法 ID，求值结果为 string |
        | ClientId | string([ExpressionField]) | 否 | "0x0001" | 客户端 ID，求值结果为 string |
        | InterfaceVersion | string([ExpressionField]) | 否 | "0x01" | 接口版本，求值结果为 string |
        | Payload | string([ExpressionField]) | 否 | 空 | 十六进制负载，可为空，求值结果为 string |
        | TimeoutMs | int | 否 | 3000 | 地址解析、连接、发送和完整响应共用总超时（ms），必须大于 0 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 存储响应负载的变量路径；如 Locals.result |

        ## 行为

        - ResultVariable 为空时跳过写入；非空须配置已声明的完整作用域 string 变量路径，当前值允许 null；建立/发送/消费响应前预检，不自动创建变量

        - 本次 UDP/TCP 临时连接登记为执行资源；中断或退出调试时取消解析、连接或通信，后台确认原始 I/O 与取消回调退出后才释放；旧资源收尾等待与建立计入原 TimeoutMs 总预算，已提交的请求不能据此视为撤回

        - ReturnCode 非 0x00 或错误响应时步骤返回 Error
        - UDP 域名异步解析与 TCP/UDP 发送均受同一个 TimeoutMs 控制，不按接收次数重置期限
        - 超时返回 Error、原调用方取消返回 Aborted；请求可能已发送时远端执行结果未知，不自动重试，真实网络调用退出后释放本次 Socket

        ## 相关插件

        - `SomeIp_FireAndForget`：无响应方法调用
        - `SomeIp_SdDiscover`：发现 SOME/IP 服务
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Ethernet", () => new SomeIpRequestExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP Request: {s.RemoteHost}:{s.RemotePort} Service={s.ServiceId} Method={s.MethodId}";
    }
}
