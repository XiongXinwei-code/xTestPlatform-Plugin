using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigureBoardLatchOperation { ConfigureBoardLatch = 0 }

[MessagePackObject(true)]
public sealed class ConfigureBoardLatchSetting : ZlgGroupedSetting
{
    public ConfigureBoardLatchOperation Operation { get; set; } = ConfigureBoardLatchOperation.ConfigureBoardLatch;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int PortSelection { get; set; } = 0;
    public int LatchMode { get; set; } = 1;
    public bool Continuous { get; set; } = false;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "ConfigureBoardLatch",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => ["ConnectionName", "TimeoutMs", "PortSelection", "LatchMode", "Continuous", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => new Operations.ConfigureBoardLatchSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PortSelection = PortSelection, LatchMode = LatchMode, Continuous = Continuous, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
