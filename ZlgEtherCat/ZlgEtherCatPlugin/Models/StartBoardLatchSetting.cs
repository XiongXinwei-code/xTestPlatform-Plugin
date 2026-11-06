using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartBoardLatchOperation { StartBoardLatch = 0 }

[MessagePackObject(true)]
public sealed class StartBoardLatchSetting : ZlgGroupedSetting
{
    public StartBoardLatchOperation Operation { get; set; } = StartBoardLatchOperation.StartBoardLatch;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public bool Continuous { get; set; } = false;
    public int Count { get; set; } = 1;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "StartBoardLatch",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => ["ConnectionName", "TimeoutMs", "Continuous", "Count", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => new Operations.StartBoardLatchSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Continuous = Continuous, Count = Count, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
