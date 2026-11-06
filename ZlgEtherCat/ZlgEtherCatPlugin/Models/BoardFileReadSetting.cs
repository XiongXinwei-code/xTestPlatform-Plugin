using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BoardFileReadOperation { BoardFileRead = 0 }

[MessagePackObject(true)]
public sealed class BoardFileReadSetting : ZlgGroupedSetting
{
    public BoardFileReadOperation Operation { get; set; } = BoardFileReadOperation.BoardFileRead;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string LocalPath { get; set; } = "\"\"";
    [ExpressionField]
    public string RemotePath { get; set; } = "\"\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "BoardFileRead",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => ["ConnectionName", "TimeoutMs", "LocalPath", "RemotePath", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => new Operations.BoardFileReadSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, LocalPath = LocalPath, RemotePath = RemotePath, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
