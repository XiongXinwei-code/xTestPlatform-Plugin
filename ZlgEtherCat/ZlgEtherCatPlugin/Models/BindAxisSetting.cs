using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BindAxisOperation { BindAxis = 0 }

[MessagePackObject(true)]
public sealed class BindAxisSetting : ZlgGroupedSetting
{
    public BindAxisOperation Operation { get; set; } = BindAxisOperation.BindAxis;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string SlaveIndex { get; set; } = "0";
    public string AllowedModes { get; set; } = "1";
    public int MaxFeedbackAgeMs { get; set; } = 100;
    public int PollIntervalMs { get; set; } = 10;
    public int InputByteOffset { get; set; } = 0;
    public int OutputByteOffset { get; set; } = 0;
    public int InputByteLength { get; set; } = 0;
    public int OutputByteLength { get; set; } = 0;
    public int StatusWordOffset { get; set; } = -1;
    public int ActualPositionOffset { get; set; } = -1;
    public int ActualVelocityOffset { get; set; } = -1;
    public int ActualTorqueOffset { get; set; } = -1;
    public int ModeDisplayOffset { get; set; } = -1;
    public int ControlWordOffset { get; set; } = -1;
    public int TargetPositionOffset { get; set; } = -1;
    public int TargetVelocityOffset { get; set; } = -1;
    public int TargetTorqueOffset { get; set; } = -1;
    public int ModeCommandOffset { get; set; } = -1;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        BindAxisOperation.BindAxis => "BindAxis",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        BindAxisOperation.BindAxis => ["ConnectionName", "TimeoutMs", "AxisName", "SlaveIndex", "AllowedModes", "MaxFeedbackAgeMs", "PollIntervalMs", "InputByteOffset", "OutputByteOffset", "InputByteLength", "OutputByteLength", "StatusWordOffset", "ActualPositionOffset", "ActualVelocityOffset", "ActualTorqueOffset", "ModeDisplayOffset", "ControlWordOffset", "TargetPositionOffset", "TargetVelocityOffset", "TargetTorqueOffset", "ModeCommandOffset", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        BindAxisOperation.BindAxis => new Operations.BindAxisSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, SlaveIndex = SlaveIndex, AllowedModes = AllowedModes, MaxFeedbackAgeMs = MaxFeedbackAgeMs, PollIntervalMs = PollIntervalMs, InputByteOffset = InputByteOffset, OutputByteOffset = OutputByteOffset, InputByteLength = InputByteLength, OutputByteLength = OutputByteLength, StatusWordOffset = StatusWordOffset, ActualPositionOffset = ActualPositionOffset, ActualVelocityOffset = ActualVelocityOffset, ActualTorqueOffset = ActualTorqueOffset, ModeDisplayOffset = ModeDisplayOffset, ControlWordOffset = ControlWordOffset, TargetPositionOffset = TargetPositionOffset, TargetVelocityOffset = TargetVelocityOffset, TargetTorqueOffset = TargetTorqueOffset, ModeCommandOffset = ModeCommandOffset, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
