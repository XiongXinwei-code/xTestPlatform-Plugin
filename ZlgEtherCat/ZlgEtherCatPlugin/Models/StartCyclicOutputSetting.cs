using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartCyclicOutputOperation { StartCyclicOutput = 0 }

[MessagePackObject(true)]
public sealed class StartCyclicOutputSetting : ZlgGroupedSetting
{
    public StartCyclicOutputOperation Operation { get; set; } = StartCyclicOutputOperation.StartCyclicOutput;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int IntervalMs { get; set; } = 10;
    public int CommandLeaseMs { get; set; } = 1000;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StartCyclicOutputOperation.StartCyclicOutput => "StartCyclicOutput",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StartCyclicOutputOperation.StartCyclicOutput => ["ConnectionName", "TimeoutMs", "IntervalMs", "CommandLeaseMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StartCyclicOutputOperation.StartCyclicOutput => new Operations.StartCyclicOutputSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, IntervalMs = IntervalMs, CommandLeaseMs = CommandLeaseMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
