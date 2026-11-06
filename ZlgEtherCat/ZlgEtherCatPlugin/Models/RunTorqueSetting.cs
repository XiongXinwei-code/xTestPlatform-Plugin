using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RunTorqueOperation { RunTorque = 0 }

[MessagePackObject(true)]
public sealed class RunTorqueSetting : ZlgGroupedSetting
{
    public RunTorqueOperation Operation { get; set; } = RunTorqueOperation.RunTorque;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string Torque { get; set; } = "0";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        RunTorqueOperation.RunTorque => "RunTorque",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        RunTorqueOperation.RunTorque => ["ConnectionName", "TimeoutMs", "AxisName", "Torque", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        RunTorqueOperation.RunTorque => new Operations.RunTorqueSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, Torque = Torque, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
