using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.DoIP.Models;

/// <summary>DoIP_VehicleDiscovery 步骤设置</summary>
[MessagePackObject(true)]
public class DoipVehicleDiscoverySetting
{
    /// <summary>广播地址（支持表达式，默认 255.255.255.255）</summary>
    [ExpressionField]
    public string BroadcastAddress { get; set; } = "\"255.255.255.255\"";

    /// <summary>DoIP UDP 发现端口（默认 13400）</summary>
    public int Port { get; set; } = 13400;

    /// <summary>等待响应超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>结果存储变量路径（存储 VIN 与逻辑地址信息，可选）</summary>
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
