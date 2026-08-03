using MessagePack;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqDoWriteSetting
{
    /// <summary>物理通道，如 "Dev1/port0/line0"</summary>
    [ExpressionField]
    public string Channel { get; set; } = string.Empty;

    /// <summary>输出值表达式（bool 或 byte）</summary>
    [ExpressionField]
    public string Value { get; set; } = string.Empty;
}
