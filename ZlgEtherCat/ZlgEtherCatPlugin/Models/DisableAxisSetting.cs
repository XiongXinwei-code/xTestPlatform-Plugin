using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisableAxisOperation { DisableAxis = 0 }

[MessagePackObject(true)]
public sealed class DisableAxisSetting : ZlgGroupedSetting
{
    public DisableAxisOperation Operation { get; set; } = DisableAxisOperation.DisableAxis;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        DisableAxisOperation.DisableAxis => "DisableAxis",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        DisableAxisOperation.DisableAxis => ["ConnectionName", "TimeoutMs", "AxisName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        DisableAxisOperation.DisableAxis => new Operations.DisableAxisSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
