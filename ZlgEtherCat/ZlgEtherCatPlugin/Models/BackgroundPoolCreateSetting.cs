using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundPoolCreateOperation { BackgroundPoolCreate = 0 }

[MessagePackObject(true)]
public sealed class BackgroundPoolCreateSetting : ZlgGroupedSetting
{
    public BackgroundPoolCreateOperation Operation { get; set; } = BackgroundPoolCreateOperation.BackgroundPoolCreate;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";
    public int MaxItems { get; set; } = 128;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BackgroundPoolCreateOperation.BackgroundPoolCreate => "BackgroundPoolCreate",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BackgroundPoolCreateOperation.BackgroundPoolCreate => ["ConnectionName", "TimeoutMs", "PoolName", "MaxItems", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BackgroundPoolCreateOperation.BackgroundPoolCreate => new Operations.BackgroundPoolCreateSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, MaxItems = MaxItems, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
