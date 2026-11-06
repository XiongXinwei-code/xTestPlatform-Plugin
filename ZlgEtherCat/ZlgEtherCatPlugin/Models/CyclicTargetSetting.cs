using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CyclicTargetOperation { WriteCspTarget = 0, WriteCsvTarget = 1, WriteCstTarget = 2 }

[MessagePackObject(true)]
public sealed class CyclicTargetSetting : ZlgGroupedSetting
{
    public CyclicTargetOperation Operation { get; set; } = CyclicTargetOperation.WriteCspTarget;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string Position { get; set; } = "0";
    [ExpressionField]
    public string Velocity { get; set; } = "0";
    [ExpressionField]
    public string Torque { get; set; } = "0";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "WriteCspTarget",
        CyclicTargetOperation.WriteCsvTarget => "WriteCsvTarget",
        CyclicTargetOperation.WriteCstTarget => "WriteCstTarget",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => ["ConnectionName", "TimeoutMs", "AxisName", "Position", "ResultVariable"],
        CyclicTargetOperation.WriteCsvTarget => ["ConnectionName", "TimeoutMs", "AxisName", "Velocity", "ResultVariable"],
        CyclicTargetOperation.WriteCstTarget => ["ConnectionName", "TimeoutMs", "AxisName", "Torque", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => new Operations.WriteCspTargetSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, Position = Position, ResultVariable = ResultVariable },
        CyclicTargetOperation.WriteCsvTarget => new Operations.WriteCsvTargetSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, Velocity = Velocity, ResultVariable = ResultVariable },
        CyclicTargetOperation.WriteCstTarget => new Operations.WriteCstTargetSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, Torque = Torque, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
