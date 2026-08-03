using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>数据采集节点项</summary>
[MessagePackObject(true)]
public class OpcUaDataAcqItem
{
    /// <summary>节点标识</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "";

    /// <summary>列名（用于CSV表头和变量标识）</summary>
    public string ColumnName { get; set; } = "";
}

/// <summary>数据导出格式</summary>
public enum DataAcqExportFormat
{
    Csv = 0,
    Variable = 1,
    Both = 2
}

/// <summary>OPC UA 数据采集启动步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaDataAcqStartSetting
{
    /// <summary>采集任务名称，用于在 Stop 步骤中引用</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "DataAcq1";

    /// <summary>OPC UA 连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "OpcUa1";

    /// <summary>要采集的节点列表</summary>
    public List<OpcUaDataAcqItem> Items { get; set; } = new();

    /// <summary>采样间隔（毫秒）</summary>
    public int SamplingIntervalMs { get; set; } = 100;

    /// <summary>最大采集时长（毫秒），0 表示无限制（需手动 Stop）</summary>
    public int MaxDurationMs { get; set; } = 0;
}
