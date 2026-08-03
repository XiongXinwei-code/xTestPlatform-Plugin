using MessagePack;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqEncoderSetting
{
    /// <summary>Counter 通道，如 "Dev1/ctr0"</summary>
    [ExpressionField]
    public string CounterChannel { get; set; } = string.Empty;

    /// <summary>解码类型</summary>
    public EncoderDecodingType DecodingType { get; set; } = EncoderDecodingType.X4;

    /// <summary>每转脉冲数 (PPR)</summary>
    public int PulsesPerRevolution { get; set; } = 1024;

    /// <summary>是否启用 Z 索引复位</summary>
    public bool ZIndexEnable { get; set; } = false;

    /// <summary>每脉冲对应的距离或角度</summary>
    public double DistancePerPulse { get; set; } = 0.3515625;

    /// <summary>输出单位</summary>
    public EncoderUnit Unit { get; set; } = EncoderUnit.Degrees;

    /// <summary>结果存入的变量名</summary>
    [ExpressionField]
    public string ResultVariable { get; set; } = string.Empty;
}
