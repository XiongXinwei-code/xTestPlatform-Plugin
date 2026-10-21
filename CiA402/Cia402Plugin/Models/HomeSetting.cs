using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class HomeSetting : AxisSetting
{
    [ExpressionField, Field("回零方式", "求值结果为整数；按设备手册配置有效方式", Required = true, Min = -128, Max = 127)] public string Method { get; set; } = "35";
	[ExpressionField, Field("搜索速度", "求值结果为整数，设备原始单位", Required = true, Min = 1, Max = 4294967295)] public string SearchVelocity { get; set; } = "100";
	[ExpressionField, Field("零点速度", "求值结果为整数，设备原始单位", Required = true, Min = 1, Max = 4294967295)] public string ZeroVelocity { get; set; } = "20";
	[ExpressionField, Field("回零加速度", "求值结果为整数，设备原始单位", Required = true, Min = 1, Max = 4294967295)] public string Acceleration { get; set; } = "100";
}
