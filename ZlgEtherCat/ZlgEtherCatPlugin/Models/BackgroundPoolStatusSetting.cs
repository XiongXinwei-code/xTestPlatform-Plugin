using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundPoolStatusOperation { BackgroundPoolStatus = 0 }

[MessagePackObject(true)]
public sealed class BackgroundPoolStatusSetting : ZlgGroupedSetting
{
    public BackgroundPoolStatusOperation Operation { get; set; } = BackgroundPoolStatusOperation.BackgroundPoolStatus;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "BackgroundPoolStatus",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => ["ConnectionName", "TimeoutMs", "PoolName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => new Operations.BackgroundPoolStatusSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
