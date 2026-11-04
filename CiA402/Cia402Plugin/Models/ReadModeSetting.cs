using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class ReadModeSetting : AxisSetting
{
    [VariablePathField]
    public string ModeVariable { get; set; } = "";
}
