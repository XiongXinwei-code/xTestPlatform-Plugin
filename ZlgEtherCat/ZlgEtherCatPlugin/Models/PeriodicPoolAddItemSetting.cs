using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PeriodicPoolAddItemOperation { PeriodicPoolAddItem = 0 }

[MessagePackObject(true)]
public sealed class PeriodicPoolAddItemSetting : ZlgGroupedSetting
{
    public PeriodicPoolAddItemOperation Operation { get; set; } = PeriodicPoolAddItemOperation.PeriodicPoolAddItem;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";
    [ExpressionField]
    public string ItemName { get; set; } = "\"Input1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public int Length { get; set; } = 1;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;
    [VariablePathField]
    public string DataVariable { get; set; } = "";
    public int MaxAgeMs { get; set; } = 1000;
    public int IntervalMs { get; set; } = 100;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        PeriodicPoolAddItemOperation.PeriodicPoolAddItem => "PeriodicPoolAddItem",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        PeriodicPoolAddItemOperation.PeriodicPoolAddItem => ["ConnectionName", "TimeoutMs", "PoolName", "ItemName", "SlaveIndex", "SlaveIndexExpression", "Offset", "Length", "DataType", "DataVariable", "MaxAgeMs", "IntervalMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        PeriodicPoolAddItemOperation.PeriodicPoolAddItem => new Operations.PeriodicPoolAddItemSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, ItemName = ItemName, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, Length = Length, DataType = DataType, DataVariable = DataVariable, MaxAgeMs = MaxAgeMs, IntervalMs = IntervalMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
