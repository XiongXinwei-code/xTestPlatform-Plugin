using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class CanCaptureSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"CAN1\"";
    [ExpressionField] public string FilterIds { get; set; } = "\"\"";
    public int DurationMs { get; set; } = 1000;
    public int SetupTimeoutMs { get; set; } = 5000;
    public int MaxFrames { get; set; } = 10000;
    [VariablePathField] public string ResultVariable { get; set; } = "";
    public bool ExportEnabled { get; set; }
    [ExpressionField] public string ExportFilePath { get; set; } = "\"\"";
    public bool OverwriteExportFile { get; set; }
    public int ExportTimeoutMs { get; set; } = 5000;
}
