using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StopBoardPulseOperation { StopBoardPulse = 0 }

[MessagePackObject(true)]
public sealed class StopBoardPulseSetting : ZlgGroupedSetting
{
    public StopBoardPulseOperation Operation { get; set; } = StopBoardPulseOperation.StopBoardPulse;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StopBoardPulseOperation.StopBoardPulse => "StopBoardPulse",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StopBoardPulseOperation.StopBoardPulse => ["ConnectionName", "TimeoutMs", "Port", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StopBoardPulseOperation.StopBoardPulse => new Operations.StopBoardPulseSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Port = Port, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
