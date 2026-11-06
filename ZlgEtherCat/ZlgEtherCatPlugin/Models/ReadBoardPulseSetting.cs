using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadBoardPulseOperation { ReadBoardPulse = 0 }

[MessagePackObject(true)]
public sealed class ReadBoardPulseSetting : ZlgGroupedSetting
{
    public ReadBoardPulseOperation Operation { get; set; } = ReadBoardPulseOperation.ReadBoardPulse;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadBoardPulseOperation.ReadBoardPulse => "ReadBoardPulse",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadBoardPulseOperation.ReadBoardPulse => ["ConnectionName", "TimeoutMs", "Port", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadBoardPulseOperation.ReadBoardPulse => new Operations.ReadBoardPulseSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Port = Port, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
