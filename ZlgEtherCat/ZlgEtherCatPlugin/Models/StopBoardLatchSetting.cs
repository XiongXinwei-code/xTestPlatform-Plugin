using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StopBoardLatchOperation { StopBoardLatch = 0 }

[MessagePackObject(true)]
public sealed class StopBoardLatchSetting : ZlgGroupedSetting
{
    public StopBoardLatchOperation Operation { get; set; } = StopBoardLatchOperation.StopBoardLatch;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StopBoardLatchOperation.StopBoardLatch => "StopBoardLatch",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StopBoardLatchOperation.StopBoardLatch => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StopBoardLatchOperation.StopBoardLatch => new Operations.StopBoardLatchSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
