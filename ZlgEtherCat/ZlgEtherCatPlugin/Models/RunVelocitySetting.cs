using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RunVelocityOperation { RunVelocity = 0 }

[MessagePackObject(true)]
public sealed class RunVelocitySetting : ZlgGroupedSetting
{
    public RunVelocityOperation Operation { get; set; } = RunVelocityOperation.RunVelocity;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string Velocity { get; set; } = "0";
    [ExpressionField]
    public string Acceleration { get; set; } = "0";
    [ExpressionField]
    public string Deceleration { get; set; } = "0";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        RunVelocityOperation.RunVelocity => "RunVelocity",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        RunVelocityOperation.RunVelocity => ["ConnectionName", "TimeoutMs", "AxisName", "Velocity", "Acceleration", "Deceleration", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        RunVelocityOperation.RunVelocity => new Operations.RunVelocitySetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, Velocity = Velocity, Acceleration = Acceleration, Deceleration = Deceleration, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
