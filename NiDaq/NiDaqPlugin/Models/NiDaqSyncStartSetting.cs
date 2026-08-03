using MessagePack;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqSyncEncoderChannel
{
    /// <summary>Counter 通道，如 "Dev1/ctr0"</summary>
    public string CounterChannel { get; set; } = string.Empty;

    /// <summary>列名标识</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>解码类型</summary>
    public EncoderDecodingType DecodingType { get; set; } = EncoderDecodingType.X4;

    /// <summary>每转脉冲数</summary>
    public int PulsesPerRevolution { get; set; } = 1024;

    /// <summary>每脉冲距离/角度</summary>
    public double DistancePerPulse { get; set; } = 0.3515625;

    /// <summary>输出单位</summary>
    public EncoderUnit Unit { get; set; } = EncoderUnit.Degrees;

    /// <summary>是否启用 Z 索引</summary>
    public bool ZIndexEnable { get; set; } = false;
}

[MessagePackObject(true)]
public class NiDaqSyncStartSetting
{
    /// <summary>采集任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>AI 通道列表</summary>
    public List<NiDaqAiChannel> AiChannels { get; set; } = new();

    /// <summary>编码器通道列表</summary>
    public List<NiDaqSyncEncoderChannel> EncoderChannels { get; set; } = new();

    /// <summary>采样率 (Hz)</summary>
    public double SampleRate { get; set; } = 1000;

    /// <summary>最大采集时长 (ms)，0 为无限</summary>
    public int MaxDurationMs { get; set; } = 0;

    /// <summary>是否使用外部触发</summary>
    public bool UseTrigger { get; set; } = false;

    /// <summary>触发源，如 "/Dev1/PFI0"</summary>
    public string TriggerSource { get; set; } = string.Empty;

    /// <summary>触发边沿</summary>
    public TriggerEdge TriggerEdge { get; set; } = TriggerEdge.Rising;

    /// <summary>导出格式</summary>
    public DaqExportFormat ExportFormat { get; set; } = DaqExportFormat.TdmsAndVariable;

    /// <summary>输出文件目录</summary>
    [ExpressionField]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>统计变量前缀</summary>
    [ExpressionField]
    public string StatVariablePrefix { get; set; } = string.Empty;

    /// <summary>读取批次大小</summary>
    public int ReadBatchSize { get; set; } = 1000;
}
