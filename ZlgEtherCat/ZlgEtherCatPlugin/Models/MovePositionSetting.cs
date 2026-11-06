using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovePositionOperation { MovePosition = 0 }

[MessagePackObject(true)]
public sealed class MovePositionSetting : ZlgGroupedSetting
{
    public MovePositionOperation Operation { get; set; } = MovePositionOperation.MovePosition;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string Position { get; set; } = "0";
    public bool Relative { get; set; } = false;
    [ExpressionField]
    public string ProfileVelocity { get; set; } = "0";
    [ExpressionField]
    public string Acceleration { get; set; } = "0";
    [ExpressionField]
    public string Deceleration { get; set; } = "0";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        MovePositionOperation.MovePosition => "MovePosition",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        MovePositionOperation.MovePosition => ["ConnectionName", "TimeoutMs", "AxisName", "Position", "Relative", "ProfileVelocity", "Acceleration", "Deceleration", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        MovePositionOperation.MovePosition => new Operations.MovePositionSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, Position = Position, Relative = Relative, ProfileVelocity = ProfileVelocity, Acceleration = Acceleration, Deceleration = Deceleration, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
