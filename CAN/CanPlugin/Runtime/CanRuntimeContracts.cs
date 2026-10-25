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

internal readonly record struct CanConfirmationBudget(long Started, int TimeoutMs, CancellationToken OriginalToken)
{
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
        if (remaining <= 0) throw new TimeoutException($"CAN 发送确认超过配置总期限 {TimeoutMs} ms；已提交票据继续追踪");
        return (int)Math.Min(int.MaxValue, Math.Ceiling(remaining));
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
}

public sealed record CommunicationSnapshot(ReceiveSnapshot Receive, CycleStatus Cycle);

public sealed record OrderedFrame(uint CobId, byte[] Data);

public sealed record CycleOptions(int PeriodMs = 10, int CycleTimeoutMs = 1000,
    int MaxLatenessMs = 100, bool RequireOnBusConfirmation = true, int InputMaxAgeMs = 1000,
    bool SuperviseFeedback = false, int FeedbackStartupGraceMs = 1000);

public sealed record MonitoringOptions(int PeriodMs = 10, int SendTimeoutMs = 1000, int MaxLatenessMs = 100);
public sealed record TimedCanPoint(int OffsetMs, OrderedFrame[] Frames);
public sealed record CanTrajectory(TimedCanPoint[] Points, int DurationMs);
public sealed record StopSdoCommand(byte NodeId, ushort Index, byte SubIndex, byte[] Data);
public sealed record ControlledStopOptions(StopSdoCommand[] Commands, int TimeoutMs = 2000);
