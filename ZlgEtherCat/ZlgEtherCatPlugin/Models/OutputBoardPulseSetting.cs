using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutputBoardPulseOperation { OutputBoardPulse = 0 }

[MessagePackObject(true)]
public sealed class OutputBoardPulseSetting : ZlgGroupedSetting
{
    public OutputBoardPulseOperation Operation { get; set; } = OutputBoardPulseOperation.OutputBoardPulse;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;
    public int PulseCount { get; set; } = 1;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "OutputBoardPulse",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => ["ConnectionName", "TimeoutMs", "Port", "PulseCount", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => new Operations.OutputBoardPulseSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Port = Port, PulseCount = PulseCount, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
