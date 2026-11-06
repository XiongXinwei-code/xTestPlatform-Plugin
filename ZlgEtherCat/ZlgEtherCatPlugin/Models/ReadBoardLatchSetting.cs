using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadBoardLatchOperation { ReadBoardLatch = 0 }

[MessagePackObject(true)]
public sealed class ReadBoardLatchSetting : ZlgGroupedSetting
{
    public ReadBoardLatchOperation Operation { get; set; } = ReadBoardLatchOperation.ReadBoardLatch;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;
    public bool Continuous { get; set; } = false;
    public int SampleIndex { get; set; } = 0;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "ReadBoardLatch",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => ["ConnectionName", "TimeoutMs", "Port", "Continuous", "SampleIndex", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => new Operations.ReadBoardLatchSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Port = Port, Continuous = Continuous, SampleIndex = SampleIndex, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
