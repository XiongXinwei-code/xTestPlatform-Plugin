using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class WriteCsvTargetSetting : AxisSetting
{
    [ExpressionField, Field("周期目标速度", "求值结果为整数，设备原始速度单位；仅发送一次 PDO", Required = true, Min = -2147483648, Max = 2147483647)] public string TargetVelocity { get; set; } = "0";
}
