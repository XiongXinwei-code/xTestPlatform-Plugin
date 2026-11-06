using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OpenOperation { Open = 0 }

[MessagePackObject(true)]
public sealed class OpenSetting : ZlgGroupedSetting
{
    public OpenOperation Operation { get; set; } = OpenOperation.Open;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string BoardSelector { get; set; } = "\"AL1\"";
    public int Channel { get; set; } = 0;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        OpenOperation.Open => "Open",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        OpenOperation.Open => ["ConnectionName", "TimeoutMs", "BoardSelector", "Channel", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        OpenOperation.Open => new Operations.OpenSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, BoardSelector = BoardSelector, Channel = Channel, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
