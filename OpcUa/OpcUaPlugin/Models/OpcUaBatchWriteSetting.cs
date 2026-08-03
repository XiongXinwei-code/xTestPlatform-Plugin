using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>批量写入的单个节点项</summary>
[MessagePackObject(true)]
public class OpcUaBatchWriteItem
{
    /// <summary>节点标识</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "";

    /// <summary>要写入的值（表达式）</summary>
    [ExpressionField]
    public string WriteValue { get; set; } = "";

    /// <summary>数据类型</summary>
    public OpcUaDataType DataType { get; set; } = OpcUaDataType.Auto;
}

/// <summary>OPC UA 批量写入步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaBatchWriteSetting
{
    /// <summary>连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "OpcUa1";

    /// <summary>要写入的节点列表</summary>
    public List<OpcUaBatchWriteItem> Items { get; set; } = new();

    /// <summary>超时时间（毫秒）</summary>
    public int TimeoutMs { get; set; } = 5000;
}
