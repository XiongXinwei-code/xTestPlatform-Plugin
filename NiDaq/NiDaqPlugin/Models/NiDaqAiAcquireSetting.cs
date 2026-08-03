using MessagePack;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqAiChannel
{
    /// <summary>物理通道，如 "Dev1/ai0"</summary>
    public string PhysicalChannel { get; set; } = string.Empty;

    /// <summary>列名标识</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>量程下限</summary>
    public double MinValue { get; set; } = -10.0;

    /// <summary>量程上限</summary>
    public double MaxValue { get; set; } = 10.0;

    /// <summary>终端配置</summary>
    public AiTerminalConfig Terminal { get; set; } = AiTerminalConfig.Differential;
}

[MessagePackObject(true)]
public class NiDaqAiAcquireSetting
{
    /// <summary>AI 通道列表</summary>
    public List<NiDaqAiChannel> Channels { get; set; } = new();

    /// <summary>采样率 (Hz)</summary>
    public double SampleRate { get; set; } = 1000;

    /// <summary>采样数（每通道）</summary>
    public int SamplesPerChannel { get; set; } = 100;

    /// <summary>结果变量前缀</summary>
    [ExpressionField]
    public string ResultVariablePrefix { get; set; } = string.Empty;
}
