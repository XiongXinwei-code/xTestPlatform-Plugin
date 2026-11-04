using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class WriteCsvTargetSetting : AxisSetting
{
    [ExpressionField]
    public string TargetVelocity { get; set; } = "0";
}
