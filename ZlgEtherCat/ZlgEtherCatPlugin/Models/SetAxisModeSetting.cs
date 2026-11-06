using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SetAxisModeOperation { SetAxisMode = 0 }

[MessagePackObject(true)]
public sealed class SetAxisModeSetting : ZlgGroupedSetting
{
    public SetAxisModeOperation Operation { get; set; } = SetAxisModeOperation.SetAxisMode;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    public int Mode { get; set; } = 1;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "SetAxisMode",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => ["ConnectionName", "TimeoutMs", "AxisName", "Mode", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => new Operations.SetAxisModeSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, Mode = Mode, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
