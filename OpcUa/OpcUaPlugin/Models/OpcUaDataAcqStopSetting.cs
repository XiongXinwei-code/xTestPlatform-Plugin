using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>OPC UA 数据采集停止步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaDataAcqStopSetting
{
    /// <summary>要停止的采集任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "DataAcq1";

    /// <summary>导出格式</summary>
    public DataAcqExportFormat ExportFormat { get; set; } = DataAcqExportFormat.Csv;

    /// <summary>CSV 导出文件路径（支持表达式）</summary>
    [ExpressionField]
    public string CsvFilePath { get; set; } = "C:\\TestData\\acq_data.csv";

    /// <summary>是否将采集数据的统计值（均值/最大/最小）存入变量</summary>
    public bool SaveStatistics { get; set; } = true;

    /// <summary>统计变量前缀（如 "Locals.Acq_"，自动追加 ColumnName_Avg/Max/Min）</summary>
    public string StatVariablePrefix { get; set; } = "Locals.Acq_";
}
