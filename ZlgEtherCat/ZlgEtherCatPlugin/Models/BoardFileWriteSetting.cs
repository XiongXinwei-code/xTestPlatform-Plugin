using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BoardFileWriteOperation { BoardFileWrite = 0 }

[MessagePackObject(true)]
public sealed class BoardFileWriteSetting : ZlgGroupedSetting
{
    public BoardFileWriteOperation Operation { get; set; } = BoardFileWriteOperation.BoardFileWrite;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string LocalPath { get; set; } = "\"\"";
    [ExpressionField]
    public string RemotePath { get; set; } = "\"\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BoardFileWriteOperation.BoardFileWrite => "BoardFileWrite",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BoardFileWriteOperation.BoardFileWrite => ["ConnectionName", "TimeoutMs", "LocalPath", "RemotePath", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BoardFileWriteOperation.BoardFileWrite => new Operations.BoardFileWriteSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, LocalPath = LocalPath, RemotePath = RemotePath, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
