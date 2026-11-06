using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnumerateBoardsOperation { EnumerateBoards = 0 }

[MessagePackObject(true)]
public sealed class EnumerateBoardsSetting : ZlgGroupedSetting
{
    public EnumerateBoardsOperation Operation { get; set; } = EnumerateBoardsOperation.EnumerateBoards;


    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => "EnumerateBoards",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => ["TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => new Operations.EnumerateBoardsSetting { TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
