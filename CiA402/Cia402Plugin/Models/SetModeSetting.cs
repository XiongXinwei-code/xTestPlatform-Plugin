using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class SetModeSetting : AxisSetting
{
    public OperationMode Mode { get; set; } = OperationMode.PP;

    public bool AllowWhileEnabled { get; set; }
}
