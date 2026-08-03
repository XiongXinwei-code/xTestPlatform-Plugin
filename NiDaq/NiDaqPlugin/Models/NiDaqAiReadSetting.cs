using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqAiReadSetting
{
    /// <summary>任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>读取样本数（每通道），-1 为读取所有可用</summary>
    public int SamplesToRead { get; set; } = -1;

    /// <summary>结果变量前缀</summary>
    [ExpressionField]
    public string ResultVariablePrefix { get; set; } = string.Empty;

    /// <summary>导出格式</summary>
    public DaqExportFormat ExportFormat { get; set; } = DaqExportFormat.Variable;

    /// <summary>输出文件目录（为空时使用默认数据目录）</summary>
    [ExpressionField]
    public string OutputDirectory { get; set; } = string.Empty;
}
