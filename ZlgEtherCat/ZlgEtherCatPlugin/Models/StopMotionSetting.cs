using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StopMotionOperation { StopMotion = 0 }

[MessagePackObject(true)]
public sealed class StopMotionSetting : ZlgGroupedSetting
{
    public StopMotionOperation Operation { get; set; } = StopMotionOperation.StopMotion;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    public bool QuickStop { get; set; } = false;
    [ExpressionField]
    public string VelocityTolerance { get; set; } = "0";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StopMotionOperation.StopMotion => "StopMotion",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StopMotionOperation.StopMotion => ["ConnectionName", "TimeoutMs", "AxisName", "QuickStop", "VelocityTolerance", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StopMotionOperation.StopMotion => new Operations.StopMotionSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, QuickStop = QuickStop, VelocityTolerance = VelocityTolerance, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
