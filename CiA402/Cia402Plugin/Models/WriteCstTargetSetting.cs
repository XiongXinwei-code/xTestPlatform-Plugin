using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class WriteCstTargetSetting : AxisSetting
{
    [ExpressionField]
    public string TargetTorque { get; set; } = "0";
}
