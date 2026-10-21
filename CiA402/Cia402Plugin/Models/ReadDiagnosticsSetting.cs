using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class ReadDiagnosticsSetting : AxisSetting
{
	[VariablePathField]
	public string ErrorCodeVariable { get; set; } = "";
	[VariablePathField]
	public string HardwareValueVariable { get; set; } = "";
	[VariablePathField]
	public string HardwareActiveVariable { get; set; } = "";
}
