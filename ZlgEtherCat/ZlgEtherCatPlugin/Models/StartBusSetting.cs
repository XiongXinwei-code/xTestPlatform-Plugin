using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartBusOperation { StartBus = 0 }

[MessagePackObject(true)]
public sealed class StartBusSetting : ZlgGroupedSetting
{
    public StartBusOperation Operation { get; set; } = StartBusOperation.StartBus;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string EniPath { get; set; } = "\"\"";
    public int ExpectedCycleUs { get; set; } = 500;
    public bool RequireDc { get; set; } = true;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StartBusOperation.StartBus => "StartBus",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StartBusOperation.StartBus => ["ConnectionName", "TimeoutMs", "EniPath", "ExpectedCycleUs", "RequireDc", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StartBusOperation.StartBus => new Operations.StartBusSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, EniPath = EniPath, ExpectedCycleUs = ExpectedCycleUs, RequireDc = RequireDc, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
