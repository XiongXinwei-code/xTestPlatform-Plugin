using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadAcquisitionStatusOperation { ReadAcquisitionStatus = 0 }

[MessagePackObject(true)]
public sealed class ReadAcquisitionStatusSetting : ZlgGroupedSetting
{
    public ReadAcquisitionStatusOperation Operation { get; set; } = ReadAcquisitionStatusOperation.ReadAcquisitionStatus;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "ReadAcquisitionStatus",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => ["ConnectionName", "TimeoutMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => new Operations.ReadAcquisitionStatusSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
