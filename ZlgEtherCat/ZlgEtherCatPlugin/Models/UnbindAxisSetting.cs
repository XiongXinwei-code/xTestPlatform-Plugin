using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnbindAxisOperation { UnbindAxis = 0 }

[MessagePackObject(true)]
public sealed class UnbindAxisSetting : ZlgGroupedSetting
{
    public UnbindAxisOperation Operation { get; set; } = UnbindAxisOperation.UnbindAxis;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        UnbindAxisOperation.UnbindAxis => "UnbindAxis",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        UnbindAxisOperation.UnbindAxis => ["ConnectionName", "TimeoutMs", "AxisName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        UnbindAxisOperation.UnbindAxis => new Operations.UnbindAxisSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
