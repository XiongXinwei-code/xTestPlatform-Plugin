using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class RunTorqueSetting : AxisSetting
{
    [ExpressionField, Field("目标转矩", "求值结果为整数；标准单位为额定转矩千分比，以设备配置为准", Required = true, Min = -32768, Max = 32767)] public string TargetTorque { get; set; } = "0";
	[ExpressionField, Field("转矩斜率", "求值结果为整数，设备原始转矩每秒", Required = true, Min = 1, Max = 4294967295)] public string TorqueSlope { get; set; } = "100";
}
