using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiDaqAoWriteSingleSetting
{
    [ExpressionField] public string TaskName { get; set; } = "\"AoTask1\"";
    [ExpressionField] public string Values { get; set; } = "";
    public int TimeoutMs { get; set; } = 10000;
}
