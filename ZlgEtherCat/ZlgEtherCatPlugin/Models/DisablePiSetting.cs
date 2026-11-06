using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisablePiOperation { DisablePi = 0 }

[MessagePackObject(true)]
public sealed class DisablePiSetting : ZlgGroupedSetting
{
    public DisablePiOperation Operation { get; set; } = DisablePiOperation.DisablePi;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        DisablePiOperation.DisablePi => "DisablePi",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        DisablePiOperation.DisablePi => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        DisablePiOperation.DisablePi => new Operations.DisablePiSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
