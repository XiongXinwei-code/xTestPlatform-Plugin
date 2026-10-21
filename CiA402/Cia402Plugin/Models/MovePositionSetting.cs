using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class MovePositionSetting : AxisSetting
{
	[ExpressionField]
	public string TargetPosition { get; set; } = "0";
	[ExpressionField]
	public string ProfileVelocity { get; set; } = "100";
	[ExpressionField]
	public string Acceleration { get; set; } = "100";
	[ExpressionField]
	public string Deceleration { get; set; } = "100";
	public bool Relative { get; set; }
	public bool WaitForTarget { get; set; } = true;
}
