using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>OPC UA 订阅步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaSubscribeSetting
{
    /// <summary>连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "OpcUa1";

    /// <summary>要订阅的节点标识</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "ns=2;s=TestComplete";

    /// <summary>期望值（达到此值时订阅完成）</summary>
    [ExpressionField]
    public string ExpectedValue { get; set; } = "true";

    /// <summary>比较模式</summary>
    public OpcUaCompareMode CompareMode { get; set; } = OpcUaCompareMode.Equal;

    /// <summary>订阅到的值存入的变量名</summary>
    public string ResultVariable { get; set; } = "Locals.SubValue";

    /// <summary>超时时间（毫秒），超时则返回 Error</summary>
    public int TimeoutMs { get; set; } = 10000;

    /// <summary>采样间隔（毫秒）</summary>
    public int SamplingIntervalMs { get; set; } = 500;
}
