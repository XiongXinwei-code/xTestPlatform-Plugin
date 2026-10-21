using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class ReadFeedbackSetting : AxisSetting
{
	[VariablePathField]
	public string StatusWordVariable { get; set; } = "";
	[VariablePathField]
	public string PositionVariable { get; set; } = "";
	[VariablePathField]
	public string VelocityVariable { get; set; } = "";
	[VariablePathField]
	public string TorqueVariable { get; set; } = "";
}
