using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundPoolStopItemOperation { BackgroundPoolStopItem = 0 }

[MessagePackObject(true)]
public sealed class BackgroundPoolStopItemSetting : ZlgGroupedSetting
{
    public BackgroundPoolStopItemOperation Operation { get; set; } = BackgroundPoolStopItemOperation.BackgroundPoolStopItem;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";
    [ExpressionField]
    public string ItemName { get; set; } = "\"Input1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BackgroundPoolStopItemOperation.BackgroundPoolStopItem => "BackgroundPoolStopItem",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BackgroundPoolStopItemOperation.BackgroundPoolStopItem => ["ConnectionName", "TimeoutMs", "PoolName", "ItemName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BackgroundPoolStopItemOperation.BackgroundPoolStopItem => new Operations.BackgroundPoolStopItemSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, ItemName = ItemName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
