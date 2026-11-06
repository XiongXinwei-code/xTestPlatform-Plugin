using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnableAxisOperation { EnableAxis = 0 }

[MessagePackObject(true)]
public sealed class EnableAxisSetting : ZlgGroupedSetting
{
    public EnableAxisOperation Operation { get; set; } = EnableAxisOperation.EnableAxis;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        EnableAxisOperation.EnableAxis => "EnableAxis",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        EnableAxisOperation.EnableAxis => ["ConnectionName", "TimeoutMs", "AxisName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        EnableAxisOperation.EnableAxis => new Operations.EnableAxisSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
