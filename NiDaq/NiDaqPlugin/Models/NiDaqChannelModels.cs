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
