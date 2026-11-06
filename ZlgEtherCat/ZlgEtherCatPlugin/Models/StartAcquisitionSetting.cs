using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartAcquisitionOperation { StartAcquisition = 0 }

[MessagePackObject(true)]
public sealed class StartAcquisitionSetting : ZlgGroupedSetting
{
    public StartAcquisitionOperation Operation { get; set; } = StartAcquisitionOperation.StartAcquisition;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int NativeWaitMs { get; set; } = 20;
    public int HistoryCapacity { get; set; } = 1024;
    public bool LatestOnly { get; set; } = false;
    public AcquisitionPriority AcquisitionThreadPriority { get; set; } = AcquisitionPriority.Normal;
    public int RxHighWatermarkPercent { get; set; } = 80;
    public int RxBacklogWarningMs { get; set; } = 10;
    public int RxAlarmRepeatMs { get; set; } = 1000;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "StartAcquisition",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => ["ConnectionName", "TimeoutMs", "NativeWaitMs", "HistoryCapacity", "LatestOnly", "AcquisitionThreadPriority", "RxHighWatermarkPercent", "RxBacklogWarningMs", "RxAlarmRepeatMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => new Operations.StartAcquisitionSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, NativeWaitMs = NativeWaitMs, HistoryCapacity = HistoryCapacity, LatestOnly = LatestOnly, AcquisitionThreadPriority = AcquisitionThreadPriority, RxHighWatermarkPercent = RxHighWatermarkPercent, RxBacklogWarningMs = RxBacklogWarningMs, RxAlarmRepeatMs = RxAlarmRepeatMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
