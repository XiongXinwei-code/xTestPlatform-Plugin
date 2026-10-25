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

    /// <summary>可选结果变量完整路径；非空时须已声明为 string（当前值允许 null），通信前预检。</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;
}
