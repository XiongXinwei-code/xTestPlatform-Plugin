using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>OPC UA 连接步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaConnectSetting
{
    /// <summary>连接名称，用于在后续步骤中引用此连接</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"OpcUa1\"";

    /// <summary>OPC UA 服务器端点 URL</summary>
    [ExpressionField]
    public string EndpointUrl { get; set; } = "\"opc.tcp://192.168.1.1:4840\"";

    /// <summary>安全策略</summary>
    public OpcUaSecurityPolicy SecurityPolicy { get; set; } = OpcUaSecurityPolicy.None;

    /// <summary>认证模式</summary>
    public OpcUaAuthMode AuthMode { get; set; } = OpcUaAuthMode.Anonymous;

    /// <summary>用户名（UserPassword 模式时使用）</summary>
    [ExpressionField]
    public string UserName { get; set; } = "\"\"";

    /// <summary>密码（UserPassword 模式时使用）</summary>
    [ExpressionField]
    public string Password { get; set; } = "\"\"";

    /// <summary>连接超时时间（毫秒）</summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>是否自动接受服务器证书</summary>
    public bool AutoAcceptCertificate { get; set; } = true;
}
