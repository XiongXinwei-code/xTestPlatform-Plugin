using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CheckEniOperation { CheckEni = 0 }

[MessagePackObject(true)]
public sealed class CheckEniSetting : ZlgGroupedSetting
{
    public CheckEniOperation Operation { get; set; } = CheckEniOperation.CheckEni;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string EniPath { get; set; } = "\"\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        CheckEniOperation.CheckEni => "CheckEni",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        CheckEniOperation.CheckEni => ["ConnectionName", "TimeoutMs", "EniPath", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        CheckEniOperation.CheckEni => new Operations.CheckEniSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, EniPath = EniPath, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
