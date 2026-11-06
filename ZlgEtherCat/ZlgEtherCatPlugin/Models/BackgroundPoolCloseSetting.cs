using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundPoolCloseOperation { BackgroundPoolClose = 0 }

[MessagePackObject(true)]
public sealed class BackgroundPoolCloseSetting : ZlgGroupedSetting
{
    public BackgroundPoolCloseOperation Operation { get; set; } = BackgroundPoolCloseOperation.BackgroundPoolClose;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BackgroundPoolCloseOperation.BackgroundPoolClose => "BackgroundPoolClose",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BackgroundPoolCloseOperation.BackgroundPoolClose => ["ConnectionName", "TimeoutMs", "PoolName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BackgroundPoolCloseOperation.BackgroundPoolClose => new Operations.BackgroundPoolCloseSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, PoolName = PoolName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
