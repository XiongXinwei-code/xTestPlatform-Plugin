using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class HomeSetting : AxisSetting
{
	[ExpressionField]
	public string Method { get; set; } = "35";
	[ExpressionField]
	public string SearchVelocity { get; set; } = "100";
	[ExpressionField]
	public string ZeroVelocity { get; set; } = "20";
	[ExpressionField]
	public string Acceleration { get; set; } = "100";
}
