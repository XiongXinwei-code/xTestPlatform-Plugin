using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AxisReadOperation { ReadAxisState = 0, ReadAxisMode = 1, ReadAxisFeedback = 2, ReadAxisDiagnostics = 3 }

[MessagePackObject(true)]
public sealed class AxisReadSetting : ZlgGroupedSetting
{
    public AxisReadOperation Operation { get; set; } = AxisReadOperation.ReadAxisState;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        AxisReadOperation.ReadAxisState => "ReadAxisState",
        AxisReadOperation.ReadAxisMode => "ReadAxisMode",
        AxisReadOperation.ReadAxisFeedback => "ReadAxisFeedback",
        AxisReadOperation.ReadAxisDiagnostics => "ReadAxisDiagnostics",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        AxisReadOperation.ReadAxisState => ["ConnectionName", "TimeoutMs", "AxisName", "ResultVariable"],
        AxisReadOperation.ReadAxisMode => ["ConnectionName", "TimeoutMs", "AxisName", "ResultVariable"],
        AxisReadOperation.ReadAxisFeedback => ["ConnectionName", "TimeoutMs", "AxisName", "ResultVariable"],
        AxisReadOperation.ReadAxisDiagnostics => ["ConnectionName", "TimeoutMs", "AxisName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        AxisReadOperation.ReadAxisState => new Operations.ReadAxisStateSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, ResultVariable = ResultVariable },
        AxisReadOperation.ReadAxisMode => new Operations.ReadAxisModeSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, ResultVariable = ResultVariable },
        AxisReadOperation.ReadAxisFeedback => new Operations.ReadAxisFeedbackSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, ResultVariable = ResultVariable },
        AxisReadOperation.ReadAxisDiagnostics => new Operations.ReadAxisDiagnosticsSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
