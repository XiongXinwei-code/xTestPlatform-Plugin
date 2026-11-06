using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResetAxisFaultOperation { ResetAxisFault = 0 }

[MessagePackObject(true)]
public sealed class ResetAxisFaultSetting : ZlgGroupedSetting
{
    public ResetAxisFaultOperation Operation { get; set; } = ResetAxisFaultOperation.ResetAxisFault;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ResetAxisFaultOperation.ResetAxisFault => "ResetAxisFault",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ResetAxisFaultOperation.ResetAxisFault => ["ConnectionName", "TimeoutMs", "AxisName", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ResetAxisFaultOperation.ResetAxisFault => new Operations.ResetAxisFaultSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
