using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class WriteCspTargetSetting : AxisSetting
{
    [ExpressionField]
    public string TargetPosition { get; set; } = "0";
}
