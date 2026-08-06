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
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        发送 SOME/IP RPC 请求并等待响应（支持 UDP/TCP）。

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
        | TimeoutMs | int | 否 | 3000 | 响应超时毫秒数 |
        | ResultVariable | string | 否 | 空 | 存储响应负载的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - ReturnCode 非 0x00 或错误响应时步骤判定为 Failed

        ## 相关插件

        - `SomeIp_FireAndForget`：无响应方法调用
        - `SomeIp_SdDiscover`：发现 SOME/IP 服务
        """;

    public override IStepExecutor CreateExecutor() => new SomeIpRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP Request: {s.RemoteHost}:{s.RemotePort} Service={s.ServiceId} Method={s.MethodId}";
    }
}
