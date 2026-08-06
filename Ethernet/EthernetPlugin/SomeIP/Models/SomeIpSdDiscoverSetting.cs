using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.SomeIP.Models;

/// <summary>SomeIp_SdDiscover 步骤设置（SOME/IP-SD 服务发现）</summary>
[MessagePackObject(true)]
public class SomeIpSdDiscoverSetting
{
    /// <summary>SD 组播地址（支持表达式，默认 224.244.224.245）</summary>
    [ExpressionField]
    public string MulticastAddress { get; set; } = "\"224.244.224.245\"";

    /// <summary>SD 端口（默认 30490）</summary>
    public int Port { get; set; } = 30490;

    /// <summary>要查找的服务 ID（支持表达式，如 0x1234；0xFFFF 表示所有服务）</summary>
    [ExpressionField]
    public string ServiceId { get; set; } = "\"0xFFFF\"";

    /// <summary>等待 OfferService 响应超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>结果存储变量路径（存储发现的服务信息，可选）</summary>
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
