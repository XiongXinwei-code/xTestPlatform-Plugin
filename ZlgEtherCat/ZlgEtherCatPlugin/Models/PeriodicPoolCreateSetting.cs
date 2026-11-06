using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PeriodicPoolCreateOperation { PeriodicPoolCreate = 0 }

[MessagePackObject(true)]
public sealed class PeriodicPoolCreateSetting : ZlgGroupedSetting
{
    public PeriodicPoolCreateOperation Operation { get; set; } = PeriodicPoolCreateOperation.PeriodicPoolCreate;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";
    public int MaxItems { get; set; } = 128;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        PeriodicPoolCreateOperation.PeriodicPoolCreate => "PeriodicPoolCreate",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        PeriodicPoolCreateOperation.PeriodicPoolCreate => ["ConnectionName", "TimeoutMs", "PoolName", "MaxItems", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        PeriodicPoolCreateOperation.PeriodicPoolCreate => new Operations.PeriodicPoolCreateSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, MaxItems = MaxItems, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
