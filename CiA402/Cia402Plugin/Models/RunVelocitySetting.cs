using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class RunVelocitySetting : AxisSetting
{
	[ExpressionField]
	public string TargetVelocity { get; set; } = "0";
	[ExpressionField]
	public string Acceleration { get; set; } = "100";
	[ExpressionField]
	public string Deceleration { get; set; } = "100";
}
