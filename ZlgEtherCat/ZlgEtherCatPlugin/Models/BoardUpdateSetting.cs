using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BoardUpdateOperation { BoardUpdate = 0 }

[MessagePackObject(true)]
public sealed class BoardUpdateSetting : ZlgGroupedSetting
{
    public BoardUpdateOperation Operation { get; set; } = BoardUpdateOperation.BoardUpdate;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string LocalPath { get; set; } = "\"\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "BoardUpdate",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => ["ConnectionName", "TimeoutMs", "LocalPath", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => new Operations.BoardUpdateSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, LocalPath = LocalPath, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
