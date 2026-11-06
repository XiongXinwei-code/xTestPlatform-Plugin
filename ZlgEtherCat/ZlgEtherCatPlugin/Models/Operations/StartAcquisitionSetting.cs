using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class StartAcquisitionSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int NativeWaitMs { get; set; } = 20;
    public int HistoryCapacity { get; set; } = 1024;
    public bool LatestOnly { get; set; } = false;
    public AcquisitionPriority AcquisitionThreadPriority { get; set; } = AcquisitionPriority.Normal;
    public int RxHighWatermarkPercent { get; set; } = 80;
    public int RxBacklogWarningMs { get; set; } = 10;
    public int RxAlarmRepeatMs { get; set; } = 1000;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if ((NativeWaitMs < 1 || NativeWaitMs > 1000)) errors.Add("单次队列等待（ms）范围为 1～1000");
        if ((HistoryCapacity < 1 || HistoryCapacity > 1000000)) errors.Add("历史缓冲容量范围为 1～1000000");
        if (!Enum.IsDefined(AcquisitionThreadPriority)) errors.Add("共享 SDK 线程优先级未定义");
        if ((RxHighWatermarkPercent < 1 || RxHighWatermarkPercent > 100)) errors.Add("RX 队列高水位（%）范围为 1～100");
        if ((RxBacklogWarningMs < 1 || RxBacklogWarningMs > 60000)) errors.Add("RX 积压告警持续时间（ms）范围为 1～60000");
        if ((RxAlarmRepeatMs < 100 || RxAlarmRepeatMs > 3600000)) errors.Add("RX 重复告警最小间隔（ms）范围为 100～3600000");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["NativeWaitMs"] = NativeWaitMs,
            ["HistoryCapacity"] = HistoryCapacity,
            ["LatestOnly"] = LatestOnly,
            ["AcquisitionThreadPriority"] = AcquisitionThreadPriority,
            ["RxHighWatermarkPercent"] = RxHighWatermarkPercent,
            ["RxBacklogWarningMs"] = RxBacklogWarningMs,
            ["RxAlarmRepeatMs"] = RxAlarmRepeatMs
        });
    }
}
