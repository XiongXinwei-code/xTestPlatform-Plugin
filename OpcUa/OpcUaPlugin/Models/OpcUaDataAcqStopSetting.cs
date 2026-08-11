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
}
