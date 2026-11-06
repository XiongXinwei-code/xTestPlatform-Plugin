using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigureReadyLostOperation { ConfigureReadyLost = 0 }

[MessagePackObject(true)]
public sealed class ConfigureReadyLostSetting : ZlgGroupedSetting
{
    public ConfigureReadyLostOperation Operation { get; set; } = ConfigureReadyLostOperation.ConfigureReadyLost;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int ReadyLostTimeoutMs { get; set; } = 0;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "ConfigureReadyLost",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => ["ConnectionName", "TimeoutMs", "ReadyLostTimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => new Operations.ConfigureReadyLostSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ReadyLostTimeoutMs = ReadyLostTimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
