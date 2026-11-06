using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WriteBoardDoOperation { WriteBoardDo = 0 }

[MessagePackObject(true)]
public sealed class WriteBoardDoSetting : ZlgGroupedSetting
{
    public WriteBoardDoOperation Operation { get; set; } = WriteBoardDoOperation.WriteBoardDo;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Value { get; set; } = 0;
    [ExpressionField]
    public string ValueExpression { get; set; } = "";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "WriteBoardDo",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => ["ConnectionName", "TimeoutMs", "Value", "ValueExpression", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => new Operations.WriteBoardDoSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Value = Value, ValueExpression = ValueExpression, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
