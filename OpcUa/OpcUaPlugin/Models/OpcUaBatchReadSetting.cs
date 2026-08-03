using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>批量读取的单个节点项</summary>
[MessagePackObject(true)]
public class OpcUaBatchReadItem
{
    /// <summary>节点标识</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "";

    /// <summary>结果存入的变量名</summary>
    public string ResultVariable { get; set; } = "";
}

/// <summary>OPC UA 批量读取步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaBatchReadSetting
{
    /// <summary>连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "OpcUa1";

    /// <summary>要读取的节点列表</summary>
    public List<OpcUaBatchReadItem> Items { get; set; } = new();

    /// <summary>超时时间（毫秒）</summary>
    public int TimeoutMs { get; set; } = 5000;
}
