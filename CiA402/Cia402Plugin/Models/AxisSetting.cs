using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public class AxisSetting
{
	[ExpressionField, Field("轴会话名", "求值结果为 string；必须先绑定该轴", Required = true, ResultType = typeof(string))]
	public string AxisName { get; set; } = "\"Axis1\"";

	[Field("步骤总超时（ms）", "包括通信和轮询的总超时", Min = 1, Max = 3600000)]
	public int TimeoutMs { get; set; } = 5000;

	[Field("轮询间隔（ms）", "非实时轮询间隔，不代表完整周期", Min = 1, Max = 60000)]
	public int PollIntervalMs { get; set; } = 20;
}
