using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

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

    /// <summary>通道/值求值后的 I/O 阶段总期限(ms)，包含旧资源收尾及 SDK；不包含表达式求值。</summary>
    public int TimeoutMs { get; set; } = 10000;
}
