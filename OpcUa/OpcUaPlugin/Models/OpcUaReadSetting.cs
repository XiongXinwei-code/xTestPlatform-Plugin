using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>OPC UA 读取节点步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaReadSetting
{
    /// <summary>连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "OpcUa1";

    /// <summary>要读取的节点标识（如 ns=2;s=Temperature）</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "ns=2;s=MyVariable";

    /// <summary>读取结果存入的变量名</summary>
    public string ResultVariable { get; set; } = "Locals.ReadValue";

    /// <summary>读取超时时间（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;
}
