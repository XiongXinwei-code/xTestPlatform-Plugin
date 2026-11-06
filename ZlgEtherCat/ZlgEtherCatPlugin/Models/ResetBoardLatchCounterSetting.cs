using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResetBoardLatchCounterOperation { ResetBoardLatchCounter = 0 }

[MessagePackObject(true)]
public sealed class ResetBoardLatchCounterSetting : ZlgGroupedSetting
{
    public ResetBoardLatchCounterOperation Operation { get; set; } = ResetBoardLatchCounterOperation.ResetBoardLatchCounter;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ResetBoardLatchCounterOperation.ResetBoardLatchCounter => "ResetBoardLatchCounter",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ResetBoardLatchCounterOperation.ResetBoardLatchCounter => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ResetBoardLatchCounterOperation.ResetBoardLatchCounter => new Operations.ResetBoardLatchCounterSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
