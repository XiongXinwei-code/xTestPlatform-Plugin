using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundPoolAddItemOperation { BackgroundPoolAddItem = 0 }

[MessagePackObject(true)]
public sealed class BackgroundPoolAddItemSetting : ZlgGroupedSetting
{
    public BackgroundPoolAddItemOperation Operation { get; set; } = BackgroundPoolAddItemOperation.BackgroundPoolAddItem;
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

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "BackgroundPoolAddItem",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => ["ConnectionName", "TimeoutMs", "PoolName", "ItemName", "SlaveIndex", "SlaveIndexExpression", "Offset", "Length", "DataType", "DataVariable", "MaxAgeMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => new Operations.BackgroundPoolAddItemSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, ItemName = ItemName, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, Length = Length, DataType = DataType, DataVariable = DataVariable, MaxAgeMs = MaxAgeMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
