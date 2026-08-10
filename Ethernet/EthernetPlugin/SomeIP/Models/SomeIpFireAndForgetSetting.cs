using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.SomeIP.Models;

/// <summary>SomeIp_FireAndForget 步骤设置（无响应方法调用）</summary>
[MessagePackObject(true)]
public class SomeIpFireAndForgetSetting
{
    /// <summary>服务端 IP 地址（支持表达式）</summary>
    [ExpressionField]
    public string RemoteHost { get; set; } = "\"192.168.1.20\"";

    /// <summary>服务端 UDP 端口（支持表达式）</summary>
    [ExpressionField]
    public string RemotePort { get; set; } = "\"30501\"";

    /// <summary>传输方式（Udp/Tcp，默认 Udp）</summary>
    public SomeIpTransport Transport { get; set; } = SomeIpTransport.Udp;

    /// <summary>服务 ID（支持表达式，如 0x1234）</summary>
    [ExpressionField]
    public string ServiceId { get; set; } = "\"0x1234\"";

    /// <summary>方法 ID（支持表达式，如 0x0001）</summary>
    [ExpressionField]
    public string MethodId { get; set; } = "\"0x0001\"";

    /// <summary>客户端 ID（支持表达式，如 0x0001）</summary>
    [ExpressionField]
    public string ClientId { get; set; } = "\"0x0001\"";

    /// <summary>接口版本（支持表达式，如 0x01）</summary>
    [ExpressionField]
    public string InterfaceVersion { get; set; } = "\"0x01\"";

    /// <summary>负载数据（十六进制，支持表达式，可为空）</summary>
    [ExpressionField]
    public string Payload { get; set; } = "\"\"";

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
