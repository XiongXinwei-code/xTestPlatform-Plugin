using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigureAutoClearOperation { ConfigureAutoClear = 0 }

[MessagePackObject(true)]
public sealed class ConfigureAutoClearSetting : ZlgGroupedSetting
{
    public ConfigureAutoClearOperation Operation { get; set; } = ConfigureAutoClearOperation.ConfigureAutoClear;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public bool Enabled { get; set; } = false;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "ConfigureAutoClear",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => ["ConnectionName", "TimeoutMs", "Enabled", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => new Operations.ConfigureAutoClearSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Enabled = Enabled, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
