using CAN.Models;

namespace CAN.Runtime;

/// <summary>可选能力：返回时该帧已实际完成发送；实现必须由厂商契约和硬件测试支持。</summary>
public interface ICanConfirmedTransmitter
{
    bool ConfirmationAvailable => true;
    void VerifyConfirmationAvailable() { if (!ConfirmationAvailable) throw new NotSupportedException("适配器发送确认能力不可用"); }
    void WriteConfirmed(CanMessage message, CancellationToken cancellationToken);
}

/// <summary>同构建内传递调用方配置的单调总预算；公开能力签名及其默认保护保持兼容。</summary>
internal interface ICanBudgetedConfirmedTransmitter
{
    void WriteConfirmed(CanMessage message, CanConfirmationBudget budget, CancellationToken cancellationToken);
}

internal interface ICanBudgetedConfirmationProbe
{
    bool HasUnresolvedTransmit { get; }
    void VerifyConfirmationAvailable(CanConfirmationBudget budget, CancellationToken ct);
}

/// <summary>同构建可选能力；全部提交后逐票确认，不改变公开单帧接口。</summary>
internal interface ICanBudgetedConfirmedBatchTransmitter
{
    bool OrderedBatchAvailable { get; }
    void VerifyOrderedBatchAvailable();
    void WriteBatchConfirmed(CanMessage[] frames, CanConfirmationBudget budget, CancellationToken ct,
        Action checkAdmission, Action<CanBatchProgress> progress, Action<CanBatchDiagnostic> publish);
}

/// <summary>有序整批提交；返回只代表 SDK 接受/适配器调用成功，绝不表示线上确认。</summary>
internal interface ICanBudgetedBatchSubmitter
{
    void VerifySubmissionAvailable();
    void WriteBatchSubmitted(CanMessage[] frames, CanConfirmationBudget budget, CancellationToken ct,
        Action checkAdmission, Action<CanBatchProgress> progress, Action<CanBatchDiagnostic> publish);
}

// NativeNotEntered is permitted only for a provably never-invoked SDK call;
// it is not cancellation/withdrawal of submitted frames. Consumers apply each event atomically.
internal enum CanBatchProgressKind { NativeAdmission, FrameConfirmed, NativeNotEntered, SubmissionCompleted }
internal enum CanBatchPurpose { Monitoring, Target }
internal readonly record struct CanBatchProgress(CanBatchProgressKind Kind, int Count);

internal readonly record struct CanConfirmationBudget(long Started, int TimeoutMs, CancellationToken OriginalToken)
{
    internal string Operation { get; init; } = "发送确认";
    internal static CanConfirmationBudget Start(int timeoutMs, CancellationToken originalToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        return new(System.Diagnostics.Stopwatch.GetTimestamp(), timeoutMs, originalToken);
    }

    internal int Remaining(CancellationToken token)
    {
        OriginalToken.ThrowIfCancellationRequested();
        token.ThrowIfCancellationRequested();
        double remaining = TimeoutMs - System.Diagnostics.Stopwatch.GetElapsedTime(Started).TotalMilliseconds;
        if (remaining <= 0) throw new TimeoutException($"CAN {Operation}超过配置总期限 {TimeoutMs} ms；已提交报文不视为撤回");
        return (int)Math.Min(int.MaxValue, Math.Ceiling(remaining));
    }

    internal void Enter(object gate, CancellationToken ct, Action? checkAdmission = null)
    {
        while (true)
        {
            checkAdmission?.Invoke();
            if (!Monitor.TryEnter(gate, Math.Min(10, Remaining(ct)))) continue;
            try { checkAdmission?.Invoke(); _ = Remaining(ct); return; }
            catch { Monitor.Exit(gate); throw; }
        }
    }
}

public interface ICanClockValidity
{
    bool IsClockCurrent(string clockId);
}

public interface ICanCloseState
{
    bool HandlesReleased { get; }
}

public interface ICanTransmitQuiescence
{
    /// <summary>只确认本连接全部发送的最终证据，不代表电机停稳；可用于隔离后的关闭。</summary>
    bool ConfirmTransmitQuiescent();
}

/// <summary>未知统计返回 null，不能用零表示驱动不提供。</summary>
public interface ICanQueueStatistics
{
    CanQueueStatistics GetQueueStatistics();
}

public sealed record CanQueueStatistics(int? DriverDepth, long? DriverOverflows,
    int? ManagedDepth, long? ManagedDropped);

public sealed record FeedbackRoute(string Name, uint CobId, int Dlc, int MaxAgeMs);
public sealed record FeedbackSnapshot(string Name, uint CobId, int ExpectedDlc, int? ActualDlc,
    string DataHex, long Sequence, long? SourceTimestampNs, string TimeDomain, string ClockId,
    long? IngressMonotonicTicks, double? AgeUpperBoundMs, bool FreshnessKnown, string Quality)
{
    public bool Valid => Quality == "Valid";
    public ulong? RawTimestamp100ns { get; init; }
    public ulong? RawTimestampUs { get; init; }
    public long? CaptureMonotonicLowerBoundTicks { get; init; }
}

public sealed record ReceiveSnapshot(Guid ConnectionEpoch, Guid AcquisitionEpoch, bool Running,
    FeedbackSnapshot[] Frames, int PendingFrames, long FifoEvictions, long LatestReplacements,
    CanQueueStatistics QueueStatistics, string? BackgroundError)
{
    public bool AllValid => Running && BackgroundError == null && Frames.All(f => f.Valid);
    public string PendingFramesScope => "CANopenRouter.PerIdFifo";
    public int RouterCapacityPerId => CanReceiveRouter.CapacityPerId;
    public string LatestReplacementsScope => "Subscription.Latest";
    public string QueueStatisticsScope => "Adapter";
}

public sealed record CycleStatus(string State, long Generation, long InputVersion, long CompletedCycles,
    long SubmittedFrames, long ConfirmedFrames, double LastDurationMs, long Overruns,
    string? FailurePhase, string? Error, bool HostExited, bool? OwnedTxQuiescent)
{
    public bool MotionActive { get; init; }
    public bool MonitoringSyncActive { get; init; }
    public bool StopCommandsAcknowledged { get; init; }
    public bool MotorStoppedVerified => false; // 通信 ACK 不能证明物理停稳。
    public int ExecutedTrajectoryPoints { get; init; }
    public int SkippedTrajectoryPoints { get; init; }
    public int HeldTrajectoryCycles { get; init; }
    public double LastTrajectoryPointLatenessMs { get; init; }
    public bool TerminalSubmitted { get; init; }
    public bool TerminalConfirmed { get; init; }
    public string MissPolicy { get; init; } = "Fail";
    public string TrajectoryClockAnchor { get; init; } = "MotionPublication";
    public long SlowMonitoringBatches { get; init; }
    public CycleTimingStatus? Timing { get; init; }
    public bool ServiceClosed { get; init; }
    public bool PartialCycle { get; init; }
    public bool ConnectionClosed { get; init; }
    public bool OwnedSendTaskExited { get; init; } = true;
    public long SubmissionCompletedFrames { get; init; }
    public bool HostSubmissionQuiescent => OwnedSendTaskExited && SubmittedFrames == SubmissionCompletedFrames;
    public long SkippedMonitoringTicks { get; init; }
    public int ConsecutiveLateMonitoringStarts { get; init; }
    public double LastMonitoringStartLatenessMs { get; init; }
    public int? MonitoringDelayTargetMs { get; init; }
    public double? LastMonitoringWaitMs { get; init; }
    public double? LastMonitoringWaitExcessMs => LastMonitoringWaitMs is double actual && MonitoringDelayTargetMs is int target
        ? Math.Max(0, actual - target) : null;
    public string MonitoringTimingMode => "IndependentStartToStart";
    public int? MonitoringPeriodMs => MonitoringDelayTargetMs;
    public double? LastMonitoringStartIntervalMs { get; init; }
    public double? LastMonitoringIntervalExcessMs => LastMonitoringStartIntervalMs is double actual && MonitoringPeriodMs is int target
        ? Math.Max(0, actual - target) : null;
    public string MonitoringStartIntervalScope => "HostBackgroundBatchStart";
    public bool MonitoringDelayPending { get; init; }
    public uint? MonitoringSyncCobId { get; init; }
    public string? MonitoringSyncDataHex { get; init; }
    public uint? CommandSyncCobId { get; init; }
    public string? CommandSyncDataHex { get; init; }
    public string SchedulingTieBreak => "LowerBatchHeadCanId";
    public string OwnedTxQuiescenceEvidence => ConnectionClosed ? "SdkConnectionClosed"
        : !OwnedSendTaskExited ? "SendTaskPending"
        : SubmittedFrames == ConfirmedFrames ? "AllFramesConfirmed" : "Unknown";
}

/// <summary>主机分段耗时及实际采用的配置；AdapterBatchMs不是总线帧的纯线缆时间。</summary>
public sealed record CycleTimingStatus(string Phase, int PeriodMs, int SendTimeoutMs, int MaxStartLatenessMs,
    double? TotalElapsedMs = null, double? DispatchDelayMs = null,
    double? AdapterBatchMs = null, double? ContinuationDelayMs = null)
{
    public double? HostPathMs => AdapterBatchMs; // Additive alias; preserve the original serialized field.
    public CanBatchDiagnostic? Batch { get; init; }
    public bool? SendTaskExited { get; init; }
    public bool DeadlineObservedWithPendingSendTask { get; init; }
    public bool? RequireOnBusConfirmation { get; init; }
    public string ConfirmationMode => RequireOnBusConfirmation switch
    { true => "OnBusConfirmation", false => "HostSubmissionOnly", null => "Unknown" };
}

public sealed record CanBatchFrameEvidence(int Index, uint CobId, string FrameType, bool IsFd, int Dlc, string DataHex,
    long Sequence, ulong Barrier100ns, bool NativeExited, bool? NativeSucceeded, bool Confirmed,
    ulong? EchoRawTimestamp100ns, long? EchoReadReturnedMonotonicTicks, long? EchoObservedMonotonicTicks);

/// <summary>Host adapter-call evidence; it does not expose native calls or SDK acceptance.</summary>
public sealed record CanAdapterSubmissionEvidence(int Index, uint CobId, string FrameType, bool IsFd, int Dlc,
    string DataHex, string CallKind, long StartedMonotonicTicks, long? ReturnedMonotonicTicks,
    bool? CallSucceeded, bool ConfirmationReported, string? Error);

/// <summary>只保存最近批次；时间段可重叠，SDK接受不代表线缆发送或电机停稳。</summary>
public sealed record CanBatchDiagnostic(Guid BatchId, string Strategy, int RequestedFrames, string Phase)
{
    public string? Purpose { get; init; }
    public Guid? AdapterEpoch { get; init; }
    public Guid? ConnectionEpoch { get; init; }
    public long? ServiceGeneration { get; init; }
    public long? MotionGeneration { get; init; }
    public double ConnectionLockWaitMs { get; init; }
    public double? AdapterLockWaitMs { get; init; }
    public long? NativeStartedMonotonicTicks { get; init; }
    public long? NativeReturnedMonotonicTicks { get; init; }
    public double? NativeSubmissionMs { get; init; }
    public double? ConfirmationWaitMs { get; init; }
    public int? NativeCallCount { get; init; }
    public int AttemptedFrames { get; init; }
    public int? KnownAcceptedFrames { get; init; }
    public bool? NativeExited { get; init; }
    public string SubmissionState { get; init; } = "NotAttempted";
    public int ConfirmedFrames { get; init; }
    public int ReportedConfirmedFrames { get; init; }
    public int SessionCompletionQueries { get; init; }
    public double SessionCompletionQueryMs { get; init; }
    public int PreflightCompletionQueries { get; init; }
    public double PreflightCompletionQueryMs { get; init; }
    public int ConfirmationCompletionQueries { get; init; }
    public double ConfirmationCompletionQueryMs { get; init; }
    public int PriorPendingAtEntry { get; init; }
    public int UnresolvedPriorFrames { get; init; }
    public bool UnresolvedPriorQueue { get; init; }
    public double? PriorTransmitDrainMs { get; init; }
    public string? Error { get; init; }
    public CanBatchFrameEvidence[] Frames { get; init; } = [];
    public CanAdapterSubmissionEvidence[] Submissions { get; init; } = [];
    public bool? AdapterCallExited { get; init; }
    public string? NativeEvidenceUnavailableReason { get; init; }
    public long StopwatchFrequency => System.Diagnostics.Stopwatch.Frequency;
}

public sealed record CommunicationSnapshot(ReceiveSnapshot Receive, CycleStatus Cycle)
{
    public string? ShutdownHistory { get; init; }
    public string? CurrentCloseError { get; init; }
    public string SnapshotStatus => CurrentCloseError != null ? "ServiceCloseFailed" : Cycle.Error != null ? "ServiceUnavailable"
        : Receive.BackgroundError != null ? "ReceiveFaulted" : Receive.AllValid ? "Ready" : "FeedbackInvalid";
    // Receive.Running describes the independent receiver, not the SYNC worker.
    public string? BackgroundError
    {
        get
        {
            var errors = new[] { Receive.BackgroundError, Cycle.Error, ShutdownHistory, CurrentCloseError }
                .Where(error => error != null).Distinct().ToArray();
            return errors.Length == 0 ? null : string.Join(Environment.NewLine, errors);
        }
    }
    public bool ServiceHealthy => Receive.Running && BackgroundError == null;
    public bool ServiceShutdownConfirmed => Cycle.ServiceClosed && Cycle.HostExited &&
        Cycle.OwnedTxQuiescent == true && !Cycle.PartialCycle && Cycle.SubmittedFrames == Cycle.ConfirmedFrames;
    public bool ServiceCleanupCompleted => Cycle.ServiceClosed && Cycle.HostExited && Cycle.HostSubmissionQuiescent && !Cycle.PartialCycle;
    public string ServiceShutdownState => ServiceShutdownConfirmed
        ? BackgroundError == null ? "Stopped" : "StoppedWithHistory"
        : ServiceCleanupCompleted ? BackgroundError == null ? "HostSubmissionStopped" : "HostSubmissionStoppedWithHistory" : "NotConfirmed";
    public string? ErrorSummary => BackgroundError == null ? null :
        $"FailurePhase={Cycle.FailurePhase ?? "无"}; LastDurationMs={Cycle.LastDurationMs:F4}; Overruns={Cycle.Overruns}; " +
        $"BatchElapsedMs={Cycle.Timing?.TotalElapsedMs?.ToString("F4") ?? "未知"}; AdapterBatchMs={Cycle.Timing?.AdapterBatchMs?.ToString("F4") ?? "未知"}; " +
        $"ConfirmationMode={Cycle.Timing?.ConfirmationMode ?? "Unknown"}; " +
        $"{(Cycle.Timing?.RequireOnBusConfirmation == false ? "发送提交期限" : "发送确认期限")}={Cycle.Timing?.SendTimeoutMs.ToString() ?? "未知"} ms; " +
        $"NativeExited={Cycle.Timing?.Batch?.NativeExited?.ToString() ?? "未知"}; SendTaskExited={Cycle.Timing?.SendTaskExited?.ToString() ?? "未知"}; " +
        $"AdapterCallExited={Cycle.Timing?.Batch?.AdapterCallExited?.ToString() ?? "未知"}; " +
        $"HostExited={Cycle.HostExited}; OwnedTxQuiescent={Cycle.OwnedTxQuiescent?.ToString() ?? "未知"}; " +
        $"DeadlineObservedWithPendingSendTask={Cycle.Timing?.DeadlineObservedWithPendingSendTask.ToString() ?? "未知"}; {BackgroundError}";
}

public sealed record OrderedFrame(uint CobId, byte[] Data);

public sealed record CycleOptions(int PeriodMs = 10, int CycleTimeoutMs = 1000,
    int MaxLatenessMs = 100, bool RequireOnBusConfirmation = true, int InputMaxAgeMs = 1000,
    bool SuperviseFeedback = false, int FeedbackStartupGraceMs = 1000)
{
    public TrajectoryMissPolicy MissPolicy { get; init; } = TrajectoryMissPolicy.Fail;
    public int LatenessWindowMs { get; init; }
    public int MaxSkippedPoints { get; init; } = 20;
    public int HoldTimeoutMs { get; init; } = 100;
}

public enum TrajectoryMissPolicy { Fail, SkipAndCatchUp, HoldLast }

public sealed record MonitoringOptions(int PeriodMs = 10, int SendTimeoutMs = 1000, int MaxLatenessMs = 100)
{
    public bool RequireOnBusConfirmation { get; init; } = true;
    public bool ResynchronizeLateMonitoring { get; init; } = true;
    public int MonitoringConsecutiveLatenessLimit { get; init; } = 3;
}
public sealed record TimedCanPoint(int OffsetMs, OrderedFrame[] Frames);
public sealed record CanTrajectory(TimedCanPoint[] Points, int DurationMs);
public sealed record StopSdoCommand(byte NodeId, ushort Index, byte SubIndex, byte[] Data);
public sealed record ControlledStopOptions(StopSdoCommand[] Commands, int TimeoutMs = 2000);
