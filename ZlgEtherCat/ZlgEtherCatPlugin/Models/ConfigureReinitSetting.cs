using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigureReinitOperation { ConfigureReinit = 0 }

[MessagePackObject(true)]
public sealed class ConfigureReinitSetting : ZlgGroupedSetting
{
    public ConfigureReinitOperation Operation { get; set; } = ConfigureReinitOperation.ConfigureReinit;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public PiReinitMode ReinitMode { get; set; } = PiReinitMode.ClearAll;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "ConfigureReinit",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => ["ConnectionName", "TimeoutMs", "ReinitMode", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => new Operations.ConfigureReinitSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ReinitMode = ReinitMode, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
