using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public class AxisSetting
{
	[ExpressionField]
	public string AxisName { get; set; } = "\"Axis1\"";

	public int TimeoutMs { get; set; } = 5000;

	public int PollIntervalMs { get; set; } = 20;
}
