using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>OPC UA 写入节点步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaWriteSetting
{
    /// <summary>连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "OpcUa1";

    /// <summary>要写入的节点标识</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "ns=2;s=MyVariable";

    /// <summary>要写入的值（表达式）</summary>
    [ExpressionField]
    public string WriteValue { get; set; } = "0";

    /// <summary>数据类型</summary>
    public OpcUaDataType DataType { get; set; } = OpcUaDataType.Auto;

    /// <summary>写入超时时间（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;
}
