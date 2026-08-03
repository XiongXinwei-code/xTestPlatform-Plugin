using MessagePack;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqAiStartSetting
{
    /// <summary>采集任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>AI 通道列表</summary>
    public List<NiDaqAiChannel> Channels { get; set; } = new();

    /// <summary>采样率 (Hz)</summary>
    public double SampleRate { get; set; } = 1000;

    /// <summary>最大采集时长 (ms)，0 为无限</summary>
    public int MaxDurationMs { get; set; } = 0;

    /// <summary>导出格式</summary>
    public DaqExportFormat ExportFormat { get; set; } = DaqExportFormat.TdmsAndVariable;

    /// <summary>输出文件目录（为空时使用默认数据目录）</summary>
    [ExpressionField]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>统计变量前缀</summary>
    [ExpressionField]
    public string StatVariablePrefix { get; set; } = string.Empty;

    /// <summary>每次从驱动读取的样本批次大小</summary>
    public int ReadBatchSize { get; set; } = 1000;
}
