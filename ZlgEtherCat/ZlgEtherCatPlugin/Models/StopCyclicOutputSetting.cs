using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StopCyclicOutputOperation { StopCyclicOutput = 0 }

[MessagePackObject(true)]
public sealed class StopCyclicOutputSetting : ZlgGroupedSetting
{
    public StopCyclicOutputOperation Operation { get; set; } = StopCyclicOutputOperation.StopCyclicOutput;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "StopCyclicOutput",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => new Operations.StopCyclicOutputSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
