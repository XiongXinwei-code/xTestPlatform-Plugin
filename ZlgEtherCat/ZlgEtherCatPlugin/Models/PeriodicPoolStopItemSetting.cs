using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PeriodicPoolStopItemOperation { PeriodicPoolStopItem = 0 }

[MessagePackObject(true)]
public sealed class PeriodicPoolStopItemSetting : ZlgGroupedSetting
{
    public PeriodicPoolStopItemOperation Operation { get; set; } = PeriodicPoolStopItemOperation.PeriodicPoolStopItem;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";
    [ExpressionField]
    public string ItemName { get; set; } = "\"Input1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "PeriodicPoolStopItem",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => ["ConnectionName", "TimeoutMs", "PoolName", "ItemName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => new Operations.PeriodicPoolStopItemSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, ItemName = ItemName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
