using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnableWatchdogOperation { EnableWatchdog = 0 }

[MessagePackObject(true)]
public sealed class EnableWatchdogSetting : ZlgGroupedSetting
{
    public EnableWatchdogOperation Operation { get; set; } = EnableWatchdogOperation.EnableWatchdog;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int WatchdogTimeoutMs { get; set; } = 1000;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        EnableWatchdogOperation.EnableWatchdog => "EnableWatchdog",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        EnableWatchdogOperation.EnableWatchdog => ["ConnectionName", "TimeoutMs", "WatchdogTimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        EnableWatchdogOperation.EnableWatchdog => new Operations.EnableWatchdogSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, WatchdogTimeoutMs = WatchdogTimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
