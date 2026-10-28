using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqDiReadSetting
{
    /// <summary>物理通道，如 "Dev1/port0/line0:7"</summary>
    [ExpressionField]
    public string Channel { get; set; } = string.Empty;

    /// <summary>结果存入的变量名</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>通道求值后的 I/O 阶段总期限(ms)，包含旧资源收尾及 SDK；不包含表达式求值。</summary>
    public int TimeoutMs { get; set; } = 10000;
}
