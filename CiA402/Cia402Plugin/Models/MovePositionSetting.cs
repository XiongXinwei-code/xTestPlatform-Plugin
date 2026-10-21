using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class MovePositionSetting : AxisSetting
{
    [ExpressionField, Field("目标位置", "求值结果为整数，设备原始位置单位", Required = true, Min = -2147483648, Max = 2147483647)] public string TargetPosition { get; set; } = "0";
	[ExpressionField, Field("轮廓速度", "求值结果为整数，设备原始速度单位", Required = true, Min = 1, Max = 4294967295)] public string ProfileVelocity { get; set; } = "100";
	[ExpressionField, Field("加速度", "求值结果为整数，设备原始单位", Required = true, Min = 1, Max = 4294967295)] public string Acceleration { get; set; } = "100";
	[ExpressionField, Field("减速度", "求值结果为整数，设备原始单位", Required = true, Min = 1, Max = 4294967295)] public string Deceleration { get; set; } = "100";
	[Field("相对位置", "相对当前位置执行")] public bool Relative { get; set; }
	[Field("等待到位", "关闭时只等待本次目标握手完成")] public bool WaitForTarget { get; set; } = true;
}
