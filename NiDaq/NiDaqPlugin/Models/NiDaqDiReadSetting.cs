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
    [ExpressionField]
    public string ResultVariable { get; set; } = string.Empty;
}
