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
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        发送 SOME/IP 无响应方法调用（RequestNoReturn，支持 UDP/TCP）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RemoteHost | 表达式(string) | 是 | "192.168.1.20" | 服务端 IP |
        | RemotePort | 表达式(string) | 是 | "30501" | 服务端端口 |
        | Transport | 枚举 | 否 | Udp | 传输方式，可选值：Udp, Tcp |
        | ServiceId | 表达式(string) | 是 | "0x1234" | 服务 ID |
        | MethodId | 表达式(string) | 是 | "0x0001" | 方法 ID |
        | ClientId | 表达式(string) | 否 | "0x0001" | 客户端 ID |
        | InterfaceVersion | 表达式(string) | 否 | "0x01" | 接口版本 |
        | Payload | 表达式(string) | 否 | 空 | 十六进制负载，可为空 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 发送成功即判定为 Passed，不等待响应

        ## 相关插件

        - `SomeIp_Request`：带响应的 RPC 请求
        """;

    public override IStepExecutor CreateExecutor() => new SomeIpFireAndForgetExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP FireAndForget: {s.RemoteHost}:{s.RemotePort} Service={s.ServiceId} Method={s.MethodId}";
    }
}
