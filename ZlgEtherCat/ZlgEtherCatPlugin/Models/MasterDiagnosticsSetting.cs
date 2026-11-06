using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MasterDiagnosticsOperation { ReadMasterStatus = 0, ReadDiagnostics = 1, ReadLinkDiagnostics = 2, ReadLastError = 3 }

[MessagePackObject(true)]
public sealed class MasterDiagnosticsSetting : ZlgGroupedSetting
{
    public MasterDiagnosticsOperation Operation { get; set; } = MasterDiagnosticsOperation.ReadMasterStatus;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "ReadMasterStatus",
        MasterDiagnosticsOperation.ReadDiagnostics => "ReadDiagnostics",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "ReadLinkDiagnostics",
        MasterDiagnosticsOperation.ReadLastError => "ReadLastError",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        MasterDiagnosticsOperation.ReadDiagnostics => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        MasterDiagnosticsOperation.ReadLinkDiagnostics => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        MasterDiagnosticsOperation.ReadLastError => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => new Operations.ReadMasterStatusSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        MasterDiagnosticsOperation.ReadDiagnostics => new Operations.ReadDiagnosticsSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        MasterDiagnosticsOperation.ReadLinkDiagnostics => new Operations.ReadLinkDiagnosticsSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        MasterDiagnosticsOperation.ReadLastError => new Operations.ReadLastErrorSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
