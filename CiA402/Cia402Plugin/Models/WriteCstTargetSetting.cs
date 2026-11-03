using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class WriteCstTargetSetting : AxisSetting
{
    [ExpressionField]
    public string TargetTorque { get; set; } = "0";
}
