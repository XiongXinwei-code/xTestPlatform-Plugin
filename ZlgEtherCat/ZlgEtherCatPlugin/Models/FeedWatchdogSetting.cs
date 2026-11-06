using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeedWatchdogOperation { FeedWatchdog = 0 }

[MessagePackObject(true)]
public sealed class FeedWatchdogSetting : ZlgGroupedSetting
{
    public FeedWatchdogOperation Operation { get; set; } = FeedWatchdogOperation.FeedWatchdog;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "FeedWatchdog",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => new Operations.FeedWatchdogSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
