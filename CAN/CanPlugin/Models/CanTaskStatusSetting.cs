using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class CanTaskStatusSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"CAN1\"";
    [ExpressionField] public string TaskName { get; set; } = "\"\"";
    [VariablePathField] public string ResultVariable { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 5000;
    public bool IncludeInactive { get; set; } = true;
}
