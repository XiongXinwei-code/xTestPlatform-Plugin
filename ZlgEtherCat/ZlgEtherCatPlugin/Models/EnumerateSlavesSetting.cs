using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnumerateSlavesOperation { EnumerateSlaves = 0 }

[MessagePackObject(true)]
public sealed class EnumerateSlavesSetting : ZlgGroupedSetting
{
    public EnumerateSlavesOperation Operation { get; set; } = EnumerateSlavesOperation.EnumerateSlaves;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "EnumerateSlaves",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => new Operations.EnumerateSlavesSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
