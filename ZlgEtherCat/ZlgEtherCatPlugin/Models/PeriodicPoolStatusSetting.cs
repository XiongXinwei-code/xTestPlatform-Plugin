using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PeriodicPoolStatusOperation { PeriodicPoolStatus = 0 }

[MessagePackObject(true)]
public sealed class PeriodicPoolStatusSetting : ZlgGroupedSetting
{
    public PeriodicPoolStatusOperation Operation { get; set; } = PeriodicPoolStatusOperation.PeriodicPoolStatus;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        PeriodicPoolStatusOperation.PeriodicPoolStatus => "PeriodicPoolStatus",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        PeriodicPoolStatusOperation.PeriodicPoolStatus => ["ConnectionName", "TimeoutMs", "PoolName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        PeriodicPoolStatusOperation.PeriodicPoolStatus => new Operations.PeriodicPoolStatusSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
