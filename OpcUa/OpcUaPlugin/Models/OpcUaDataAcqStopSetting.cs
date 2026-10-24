using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>OPC UA 数据采集停止步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaDataAcqStopSetting
{
    /// <summary>要停止的采集任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"DataAcq1\"";

    /// <summary>名称求值后的停止阶段总期限(ms)，包含排队及真实收尾，不包含表达式求值。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
