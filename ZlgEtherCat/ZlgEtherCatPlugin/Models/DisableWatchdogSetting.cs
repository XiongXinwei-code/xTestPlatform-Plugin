using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisableWatchdogOperation { DisableWatchdog = 0 }

[MessagePackObject(true)]
public sealed class DisableWatchdogSetting : ZlgGroupedSetting
{
    public DisableWatchdogOperation Operation { get; set; } = DisableWatchdogOperation.DisableWatchdog;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        DisableWatchdogOperation.DisableWatchdog => "DisableWatchdog",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        DisableWatchdogOperation.DisableWatchdog => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        DisableWatchdogOperation.DisableWatchdog => new Operations.DisableWatchdogSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
