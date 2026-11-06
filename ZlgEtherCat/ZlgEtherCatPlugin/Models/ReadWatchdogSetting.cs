using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadWatchdogOperation { ReadWatchdog = 0 }

[MessagePackObject(true)]
public sealed class ReadWatchdogSetting : ZlgGroupedSetting
{
    public ReadWatchdogOperation Operation { get; set; } = ReadWatchdogOperation.ReadWatchdog;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadWatchdogOperation.ReadWatchdog => "ReadWatchdog",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadWatchdogOperation.ReadWatchdog => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadWatchdogOperation.ReadWatchdog => new Operations.ReadWatchdogSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
