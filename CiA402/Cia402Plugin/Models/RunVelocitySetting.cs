using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class RunVelocitySetting : AxisSetting
{
    [ExpressionField, Field("目标速度", "求值结果为整数，设备原始速度单位", Required = true, Min = -2147483648, Max = 2147483647)] public string TargetVelocity { get; set; } = "0";
	[ExpressionField, Field("加速度", "求值结果为整数，设备原始单位", Required = true, Min = 1, Max = 4294967295)] public string Acceleration { get; set; } = "100";
	[ExpressionField, Field("减速度", "求值结果为整数，设备原始单位", Required = true, Min = 1, Max = 4294967295)] public string Deceleration { get; set; } = "100";
}
