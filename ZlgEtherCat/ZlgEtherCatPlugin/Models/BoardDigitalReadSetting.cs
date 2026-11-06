using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BoardDigitalReadOperation { ReadBoardDi = 0, ReadBoardDo = 1 }

[MessagePackObject(true)]
public sealed class BoardDigitalReadSetting : ZlgGroupedSetting
{
    public BoardDigitalReadOperation Operation { get; set; } = BoardDigitalReadOperation.ReadBoardDi;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "ReadBoardDi",
        BoardDigitalReadOperation.ReadBoardDo => "ReadBoardDo",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        BoardDigitalReadOperation.ReadBoardDo => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => new Operations.ReadBoardDiSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        BoardDigitalReadOperation.ReadBoardDo => new Operations.ReadBoardDoSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
