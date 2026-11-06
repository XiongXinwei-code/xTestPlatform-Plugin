using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlaveDiagnosticsOperation { ReadSlaveInfo = 0, ReadSlaveStatus = 1 }

[MessagePackObject(true)]
public sealed class SlaveDiagnosticsSetting : ZlgGroupedSetting
{
    public SlaveDiagnosticsOperation Operation { get; set; } = SlaveDiagnosticsOperation.ReadSlaveInfo;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        SlaveDiagnosticsOperation.ReadSlaveInfo => "ReadSlaveInfo",
        SlaveDiagnosticsOperation.ReadSlaveStatus => "ReadSlaveStatus",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        SlaveDiagnosticsOperation.ReadSlaveInfo => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "ResultVariable"],
        SlaveDiagnosticsOperation.ReadSlaveStatus => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        SlaveDiagnosticsOperation.ReadSlaveInfo => new Operations.ReadSlaveInfoSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, ResultVariable = ResultVariable },
        SlaveDiagnosticsOperation.ReadSlaveStatus => new Operations.ReadSlaveStatusSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
