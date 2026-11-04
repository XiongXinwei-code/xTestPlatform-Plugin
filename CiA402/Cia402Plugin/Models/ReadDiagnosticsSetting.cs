using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
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
