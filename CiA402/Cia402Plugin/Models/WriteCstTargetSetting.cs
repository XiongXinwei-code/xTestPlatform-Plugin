using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class WriteCstTargetSetting : AxisSetting
{
    [ExpressionField, Field("周期目标转矩", "求值结果为整数，额定转矩千分比；仅发送一次 PDO", Required = true, Min = -32768, Max = 32767)] public string TargetTorque { get; set; } = "0";
}
