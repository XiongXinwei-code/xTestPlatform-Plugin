using System.Diagnostics;
using CAN.CANopen;
using PluginCommon;

namespace CAN.Runtime;

/// <summary>一个独立发送源；连接以完整批次FIFO串行提交多个源，后台不求值。</summary>
public sealed class CanopenCyclicService : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private readonly CanConnectionResource _connection;
    private readonly FeedbackSubscription _subscription;
    private readonly Dictionary<byte, CanopenNodeAccess> _nodes = [];
    private CancellationTokenSource? _stop;
    private CancellationTokenSource? _targetQueueStop;
    private Task _targetQueueCancellation = Task.CompletedTask;
    private Task _worker = Task.CompletedTask, _cancelCallbacks = Task.CompletedTask;
    private sealed record MotionStopResult(long Generation, bool Stopped, bool PartialCycle,
        bool HostSendSettled, bool StopCommandsAcknowledged, string? Error);
    private TaskCompletionSource<MotionStopResult> _motionStopped = MotionSignal();
    private OrderedFrame[] _targets = [];
    private OrderedFrame? _sync;
    private OrderedFrame? _monitorSync;
    private MonitoringOptions? _monitor;
    private CycleOptions _options = new();
    private ControlledStopOptions? _controlledStop;
    private CanTrajectory? _trajectory;
    private TrajectoryCursor? _trajectoryCursor;
    private TrajectoryCursor.Selection? _trajectorySelection;
    private bool _trajectorySelectionSubmitted, _trajectoryClockStarted, _terminalSubmitted;
    private long _generation, _motionGeneration, _version, _publishedAt, _motionStart, _nextTarget, _nextSync;
    private long _cycles, _submitted, _confirmed, _overruns;
    private long _completedSubmissions;
    private string _state = "Idle";
    private string? _phase, _error;
    private double _lastDuration;
    private long _slowMonitoringBatches;
    private long _skippedMonitoringTicks;
    private int _consecutiveLateMonitoringStarts;
    private double _lastMonitoringStartLatenessMs;
    private long? _monitorDelayStartedAt;
    private double? _lastMonitoringWaitMs;
    private long? _lastBackgroundStartedAt;
    private double? _lastMonitoringStartIntervalMs;
    private TaskCompletionSource _scheduleChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _monitoringDelayPending;
    private CycleTimingStatus? _lastTiming;
    private Exception? _closeHistory;
    private string? _currentCloseError;
    internal Exception? CloseHistory { get { lock (_gate) return _closeHistory; } }
    private bool _closed, _closing, _faulted, _moving, _stopMotion, _partialCycle;
    private bool _stopAcknowledged, _stopCommandAttempted;
    private bool _sendTaskInFlight;
    private int _pointIndex;
    internal bool IsClosed { get { lock (_gate) return _closed; } }
    internal long DiagnosticGeneration { get { lock (_gate) return _generation; } }
    internal long DiagnosticMotionGeneration { get { lock (_gate) return _motionGeneration; } }
    internal void RecordSendFailure(Exception error)
    {
        lock (_gate) { _faulted = true; _state = "Faulted"; _error ??= error.ToString(); _stopMotion = true; }
    }
    internal void ValidateSendBatch(OrderedFrame[] frames, CanBatchPurpose purpose)
    {
        lock (_gate)
        {
            var expectedSync = purpose switch
            { CanBatchPurpose.Monitoring => _monitorSync, CanBatchPurpose.Target => _sync, _ => null };
            if (expectedSync == null || frames.Length == 0 || frames.Length > 129 ||
                frames.Any(f => f == null || f.Data == null) ||
                frames[^1].CobId != expectedSync.CobId || !frames[^1].Data.SequenceEqual(expectedSync.Data) ||
                frames.Select(f => f.CobId).Distinct().Count() != frames.Length)
                throw new ArgumentException("周期整批要求唯一标准ID、最后一个成员为本服务配置的SYNC，未提交任何帧");
            if (purpose == CanBatchPurpose.Monitoring && frames.Length != 1)
                throw new ArgumentException("背景批次只能包含配置的单个背景 SYNC");
            if (purpose == CanBatchPurpose.Target)
            {
                if (!_moving) throw new InvalidOperationException("目标批次没有当前运动所有者");
                ValidateLayout(_targets, frames[..^1]);
            }
        }
    }

    private static TaskCompletionSource<MotionStopResult> MotionSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static long Ticks(int ms) => checked((long)(ms * (Stopwatch.Frequency / 1000d)));
    private static long Ticks(double ms) => checked((long)(ms * (Stopwatch.Frequency / 1000d)));
    internal CanopenCyclicService(CanConnectionResource connection, FeedbackSubscription subscription)
        => (_connection, _subscription) = (connection, subscription);

    public CommunicationSnapshot Snapshot()
    {
        CycleStatus cycle; bool running; string? closeHistory, closeError;
        bool connectionClosed = _connection.IsClosed;
        lock (_gate)
        {
            running = !_closed;
            closeHistory = _closeHistory?.ToString();
            closeError = _currentCloseError;
            cycle = new(_state, _generation, _version, _cycles, _submitted, _confirmed, _lastDuration,
                _overruns, _phase, _error, _worker.IsCompleted && _cancelCallbacks.IsCompleted && _targetQueueCancellation.IsCompleted,
                (!_sendTaskInFlight && _submitted == _confirmed) || connectionClosed ? true : null)
            { MotionActive = _moving, MonitoringSyncActive = _monitor != null && !_worker.IsCompleted && !_faulted,
                StopCommandsAcknowledged = _stopAcknowledged, ExecutedTrajectoryPoints = _pointIndex,
                SubmissionCompletedFrames = _completedSubmissions,
                SkippedTrajectoryPoints = _trajectoryCursor?.SkippedPoints ?? 0,
                HeldTrajectoryCycles = _trajectoryCursor?.HeldCycles ?? 0,
                LastTrajectoryPointLatenessMs = _trajectoryCursor?.LastLatenessMs ?? 0,
                TerminalSubmitted = _trajectoryCursor?.TerminalSubmitted ?? _terminalSubmitted,
                TerminalConfirmed = _options.RequireOnBusConfirmation && (_trajectoryCursor?.TerminalSubmitted ?? _terminalSubmitted),
                MissPolicy = _options.MissPolicy.ToString(),
                TrajectoryClockAnchor = _trajectoryCursor == null ? "MotionPublication" : "FirstTargetBatchAdmission",
                SlowMonitoringBatches = _slowMonitoringBatches, Timing = _lastTiming == null ? null : _lastTiming with
                { Batch = _lastTiming.Batch is { } evidence ? evidence with { Frames = evidence.Frames.ToArray(), Submissions = evidence.Submissions.ToArray() } : null },
                ServiceClosed = _closed, PartialCycle = _partialCycle, ConnectionClosed = connectionClosed,
                OwnedSendTaskExited = !_sendTaskInFlight, SkippedMonitoringTicks = _skippedMonitoringTicks,
                ConsecutiveLateMonitoringStarts = _consecutiveLateMonitoringStarts, LastMonitoringStartLatenessMs = _lastMonitoringStartLatenessMs,
                MonitoringDelayTargetMs = _monitor?.PeriodMs, LastMonitoringWaitMs = _lastMonitoringWaitMs,
                LastMonitoringStartIntervalMs = _lastMonitoringStartIntervalMs,
                MonitoringDelayPending = _monitoringDelayPending };
            cycle = cycle with { MonitoringSyncCobId = _monitorSync?.CobId,
                MonitoringSyncDataHex = _monitorSync == null ? null : Convert.ToHexString(_monitorSync.Data),
                CommandSyncCobId = _sync?.CobId, CommandSyncDataHex = _sync == null ? null : Convert.ToHexString(_sync.Data) };
        }
        return new(_connection.Snapshot(_subscription, running), cycle) { ShutdownHistory = closeHistory, CurrentCloseError = closeError };
    }

    private static OrderedFrame[] CopyFrames(OrderedFrame[] frames)
    {
        if (frames == null || frames.Length is 0 or > 128 ||
            frames.Any(f => f == null || f.CobId > 0x7FF || f.Data == null || f.Data.Length > 8) ||
            frames.Select(f => f.CobId).Distinct().Count() != frames.Length)
            throw new ArgumentException("RPDO 必须为 1~128 个唯一标准 CAN ID，数据长度 0~8");
        return frames.Select(f => f with { Data = f.Data.ToArray() }).ToArray();
    }
    private void CheckStart()
    {
        if (_closed || _closing || _faulted || _stopMotion || _moving ||
            (_monitor == null && (_stop != null || !_worker.IsCompleted)))
            throw new InvalidOperationException("旧发送尚未确认停止或服务已故障/关闭");
    }
    private void CheckConfirmation(bool require, int timeoutMs, CancellationToken ct = default, bool orderedBatch = false)
    {
        if (!require) { _connection.VerifySubmissionAvailable(); return; }
        if (!_connection.SupportsConfirmedTransmit)
            throw new NotSupportedException("UnsupportedGuarantee：发送确认能力当前不可用，未发送目标（可能是硬件能力不足、连接正在退出或旧发送未决）；旧发送未知时须受控 CAN_Close 完成后重新 CAN_Open，不能仅重建周期服务");
        _connection.VerifyConfirmationAvailable(timeoutMs, ct, orderedBatch);
    }
    private static void ValidateSync(OrderedFrame sync)
    {
        if (sync == null || sync.CobId > 0x7FF || sync.Data == null || sync.Data.Length > 1)
            throw new ArgumentException("SYNC 必须为标准 CAN，DLC 0 或 1");
    }

    private static ControlledStopOptions? CopyStop(ControlledStopOptions? options)
    {
        if (options != null && (options.TimeoutMs <= 0 || options.Commands == null || options.Commands.Length is 0 or > 32 ||
            options.Commands.Any(c => c == null || c.NodeId is < 1 or > 127 || c.Data == null || c.Data.Length is < 1 or > 4)))
            throw new ArgumentException("停机命令要求 1~32 条、节点 1~127、每条 1~4 字节和正数总期限");
        return options == null ? null : options with { Commands = options.Commands.Select(c => c with { Data = c.Data.ToArray() }).ToArray() };
    }

    public void ConfigureControlledStop(ControlledStopOptions? options)
    {
        var copy = CopyStop(options);
        lock (_gate)
        {
            if (_moving || _closing || _closed || _faulted) throw new InvalidOperationException("仅能在运动前配置停机命令");
            _controlledStop = copy;
        }
    }

    public void StartMonitoring(OrderedFrame sync, MonitoringOptions options, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var budget = Stopwatch.StartNew();
        ValidateSync(sync);
        if (options.PeriodMs <= 0 || options.SendTimeoutMs <= 0 || options.MaxLatenessMs < 0 || options.MonitoringConsecutiveLatenessLimit is < 1 or > 10000)
            throw new ArgumentException("监测周期参数无效");
        lock (_gate) { CheckStart(); if (_monitor != null) throw new InvalidOperationException("监测已启动"); }
        using var startAdmission = _connection.EnterSenderStart(this, options.RequireOnBusConfirmation);
        CheckConfirmation(options.RequireOnBusConfirmation, options.SendTimeoutMs, ct);
        lock (_gate)
        {
            ct.ThrowIfCancellationRequested();
            if (budget.ElapsedMilliseconds >= options.SendTimeoutMs) throw new TimeoutException("监测启动总期限已到，未发布 SYNC");
            CheckStart();
            if (_monitor != null) throw new InvalidOperationException("监测已启动");
            _connection.ReserveSender(this, [sync.CobId], [sync.CobId], options.RequireOnBusConfirmation);
            _monitorSync = sync with { Data = sync.Data.ToArray() }; _monitor = options;
            _consecutiveLateMonitoringStarts = 0;
            _monitorDelayStartedAt = null; _lastMonitoringWaitMs = null;
            _lastBackgroundStartedAt = null; _lastMonitoringStartIntervalMs = null;
            _nextSync = Stopwatch.GetTimestamp(); _state = "Monitoring"; _generation++;
            StartWorker();
        }
    }

    public void StartTargets(byte[] nodes, OrderedFrame[] targets, OrderedFrame sync, CycleOptions options)
        => StartMotion(nodes, targets, sync, options, null);

    public void StartConfiguredTargets(byte[] nodes, OrderedFrame[] targets, OrderedFrame sync, CycleOptions options, ControlledStopOptions? stop, CancellationToken ct = default)
        => StartMotion(nodes, targets, sync, options, null, stop, true, ct);

    public void StartConfiguredTrajectory(byte[] nodes, CanTrajectory trajectory, OrderedFrame sync, CycleOptions options, ControlledStopOptions? stop, CancellationToken ct = default)
    {
        var copy = CopyTrajectory(trajectory);
        StartMotion(nodes, copy.Points[0].Frames, sync, options, copy, stop, true, ct);
    }

    public void StartTrajectory(byte[] nodes, CanTrajectory trajectory, OrderedFrame sync, CycleOptions options)
    {
        var copy = CopyTrajectory(trajectory);
        StartMotion(nodes, copy.Points[0].Frames, sync, options, copy);
    }

    private static CanTrajectory CopyTrajectory(CanTrajectory trajectory)
    {
        if (trajectory.Points == null || trajectory.Points.Length is 0 or > 100000 || trajectory.DurationMs <= 0)
            throw new ArgumentException("有限轨迹要求 1~100000 个点和正数持续时间");
        var points = trajectory.Points.Select(p => new TimedCanPoint(p.OffsetMs, CopyFrames(p.Frames))).ToArray();
        for (var i = 0; i < points.Length; i++)
        {
            if (points[i].OffsetMs < 0 || points[i].OffsetMs >= trajectory.DurationMs ||
                (i == 0 ? points[i].OffsetMs != 0 : points[i].OffsetMs <= points[i - 1].OffsetMs))
                throw new ArgumentException("轨迹时间必须从 0 开始、严格递增且早于结束时间");
            ValidateLayout(points[0].Frames, points[i].Frames);
        }
        return new(points, trajectory.DurationMs);
    }

    private void StartMotion(byte[] nodes, OrderedFrame[] targets, OrderedFrame sync, CycleOptions options, CanTrajectory? trajectory,
        ControlledStopOptions? stopConfig = null, bool replaceStop = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var budget = Stopwatch.StartNew();
        int Remaining()
        {
            ct.ThrowIfCancellationRequested();
            int remaining = options.CycleTimeoutMs - (int)Math.Min(int.MaxValue, budget.ElapsedMilliseconds);
            return remaining > 0 ? remaining : throw new TimeoutException("目标启动总期限已到，未发布目标");
        }
        var stopCopy = replaceStop ? CopyStop(stopConfig) : null;
        var copy = CopyFrames(targets); ValidateSync(sync);
        if (copy.Any(f => f.CobId == sync.CobId) || options.PeriodMs <= 0 || options.CycleTimeoutMs <= 0 ||
            options.MaxLatenessMs < 0 || options.InputMaxAgeMs <= 0 || options.FeedbackStartupGraceMs < 0 ||
            !Enum.IsDefined(options.MissPolicy) || options.LatenessWindowMs < 0 || options.MaxSkippedPoints < 0 || options.HoldTimeoutMs <= 0 ||
            nodes.Length == 0 || nodes.Any(n => n is < 1 or > 127) || nodes.Distinct().Count() != nodes.Length)
            throw new ArgumentException("节点、周期、有效期或超时配置无效");
        lock (_gate) CheckStart();
        using var startAdmission = _connection.EnterSenderStart(this, options.RequireOnBusConfirmation);
        CheckConfirmation(options.RequireOnBusConfirmation, Remaining(), ct, true);
        if (!_tickGate.Wait(Remaining(), ct)) throw new TimeoutException("等待监测周期退出超时，未发布目标");
        try
        {
            lock (_gate)
            {
                CheckStart();
                _ = Remaining();
                var selectedStop = replaceStop ? stopCopy : _controlledStop;
                if (trajectory != null && options.MissPolicy == TrajectoryMissPolicy.HoldLast && selectedStop == null)
                    throw new ArgumentException("HoldLast 必须配置经设备审查的有限停机命令，不能无限保持目标");
                if (_monitor != null && _monitor.RequireOnBusConfirmation != options.RequireOnBusConfirmation)
                    throw new ArgumentException("逐批确认诊断与周期仅提交不能在同一发送源混跑，请先停止该源");
                if (_monitor != null && options.RequireOnBusConfirmation && selectedStop == null)
                    throw new InvalidOperationException("监测 SYNC 下启动运动必须先配置经设备审查的停机命令，不能只停止目标后继续激活旧目标");
                if (_monitorSync != null && copy.Any(f => f.CobId == _monitorSync.CobId))
                    throw new ArgumentException("RPDO COB-ID 不能与背景 SYNC 冲突，未发布目标");
                if (selectedStop?.Commands.Any(c => !nodes.Contains(c.NodeId)) == true)
                    throw new ArgumentException("停机命令只能访问当前控制所有者占用的节点");
                long ownedGeneration = _generation + 1;
                ExecutionScopedExecutor.Own(() => RetireMotion(ownedGeneration));
                try
                {
                    foreach (var node in nodes)
                        _nodes.Add(node, CanopenConnectionAccess.For(_connection).ReserveNodeForSender(node, Remaining()));
                    _ = Remaining();
                    var ids = copy.Select(f => f.CobId).Append(sync.CobId)
                        .Concat(_monitorSync == null ? [] : new[] { _monitorSync.CobId }).Distinct().ToArray();
                    uint[] syncIds = _monitorSync == null ? [sync.CobId] : [sync.CobId, _monitorSync.CobId];
                    if (_monitor == null) _connection.ReserveSender(this, ids, syncIds, options.RequireOnBusConfirmation);
                    else _connection.UpdateSender(this, ids, syncIds, options.RequireOnBusConfirmation);
                }
                catch { ReleaseNodes(); throw; }
                _controlledStop = selectedStop;
                _consecutiveLateMonitoringStarts = 0;
                _targets = copy; _sync = sync with { Data = sync.Data.ToArray() }; _options = options; _trajectory = trajectory;
                _trajectoryCursor = trajectory != null && options.MissPolicy != TrajectoryMissPolicy.Fail ? new(trajectory, options) : null;
                _trajectorySelection = null; _trajectorySelectionSubmitted = false; _trajectoryClockStarted = false;
                _terminalSubmitted = false;
                _targetQueueStop?.Dispose(); _targetQueueStop = new(); _targetQueueCancellation = Task.CompletedTask;
                _pointIndex = 0;
                _stopCommandAttempted = false;
                _moving = true; _stopMotion = false; _motionStopped = MotionSignal(); _partialCycle = false; _stopAcknowledged = false;
                _motionStart = _publishedAt = _nextTarget = Stopwatch.GetTimestamp();
                _version++; _generation++; _motionGeneration = _generation; _state = "Running"; _error = _phase = null;
                WakeSchedule();
                if (_monitor == null) { _cycles = _submitted = _confirmed = _completedSubmissions = _overruns = 0; StartWorker(); }
            }
        }
        finally { _tickGate.Release(); }
    }
    private void StartWorker()
    {
        _stop = new(); var token = _stop.Token;
        long workerGeneration = _generation;
        ExecutionScopedExecutor.Own(() => RetireWorkerStartup(workerGeneration));
        _worker = Task.Run(() => RunAsync(token));
    }
    private void RetireWorkerStartup(long generation)
    {
        lock (_gate)
        {
            if (_generation != generation) return;
            // Retiring a staged Start must keep its reviewed stop path alive.
            // Cancelling the worker first would skip CompleteMotionAsync entirely.
            if (_moving) { _stopMotion = true; _state = "Stopping"; WithdrawQueuedTargets(); WakeSchedule(); }
            else RequestStop();
        }
    }
    private void RetireMotion(long generation)
    {
        lock (_gate)
        {
            if (!_moving || _motionGeneration != generation) return;
            _stopMotion = true; _state = "Stopping";
            WithdrawQueuedTargets();
            WakeSchedule();
        }
    }
    private static void ValidateLayout(OrderedFrame[] expected, OrderedFrame[] actual)
    {
        if (actual.Length != expected.Length || actual.Where((f, i) => f.CobId != expected[i].CobId || f.Data.Length != expected[i].Data.Length).Any())
            throw new ArgumentException("不能改变 RPDO 数量、顺序、COB-ID 或 DLC");
    }
    public void PublishTargets(OrderedFrame[] targets)
    {
        var copy = CopyFrames(targets);
        lock (_gate)
        {
            if (!_moving || _stopMotion || _trajectory != null || _stop?.IsCancellationRequested != false)
                throw new InvalidOperationException("实时目标未运行、已冻结或正在执行不可变轨迹");
            ValidateLayout(_targets, copy); _targets = copy; _publishedAt = Stopwatch.GetTimestamp(); _version++;
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                await _tickGate.WaitAsync(ct).ConfigureAwait(false);
                var delay = 1d;
                Task scheduleWake = Task.CompletedTask;
                try
                {
                    bool stopMotion, target; long due; CycleOptions options;
                    lock (_gate)
                    {
                        if (_closing && !_moving) break;
                        options = _options;
                        // Capture before releasing tick ownership; later motion
                        // publication cannot pulse before this waiter subscribes.
                        scheduleWake = _scheduleChanged.Task;
                        if (_moving && _trajectory != null && (_trajectoryCursor == null || _trajectoryClockStarted) &&
                            Stopwatch.GetElapsedTime(_motionStart).TotalMilliseconds >= _trajectory.DurationMs)
                        {
                            if (_trajectoryCursor != null ? !_trajectoryCursor.Complete : _pointIndex < _trajectory.Points.Length)
                            {
                                _phase = "TrajectoryEndDeadline";
                                if (_trajectoryCursor != null) throw new TimeoutException("轨迹总时长已到，终点未完整提交；禁止在期限外强发终点");
                                throw new TimeoutException("轨迹结束期限已到，但存在未执行时间点；禁止跳点或补发");
                            }
                            _stopMotion = true;
                        }
                        stopMotion = _moving && _stopMotion;
                        // Due-time FIFO; only an exact logical tie compares
                        // transmission-unit head IDs. Never sort RPDO/SYNC members.
                        target = _moving && (_monitor == null || _nextTarget < _nextSync ||
                            (_nextTarget == _nextSync && _targets[0].CobId < _monitorSync!.CobId));
                        due = target ? _nextTarget : _nextSync;
                    }
                    if (stopMotion)
                    {
                        await CompleteMotionAsync(ct).ConfigureAwait(false);
                        if (_monitor == null) break;
                        continue;
                    }
                    if (!_moving && _monitor == null) break;
                    if (_moving) CheckSupervision();
                    lock (_gate) if (_stopMotion) continue;
                    long checkingAt = Stopwatch.GetTimestamp();
                    var remaining = (due - checkingAt) * 1000d / Stopwatch.Frequency;
                    // Background SYNC and target+SYNC have independent due times.
                    // Neither is derived from device-frame timestamps.
                    if (remaining > 0) delay = !_moving && _monitor != null ? remaining : Math.Min(remaining, 5);
                    else
                    {
                        var limit = target ? options.MaxLatenessMs : _monitor!.MaxLatenessMs;
                        lock (_gate)
                        {
                            if (!target)
                            {
                                _lastMonitoringStartLatenessMs = Math.Max(0, -remaining);
                                _lastMonitoringWaitMs = _monitorDelayStartedAt is long start
                                    ? Stopwatch.GetElapsedTime(start, checkingAt).TotalMilliseconds : null;
                            }
                            if (-remaining > limit && !(target && _trajectoryCursor != null && _trajectoryClockStarted))
                            {
                                _phase = target ? "TargetStartLateness" : "MonitoringStartLateness";
                                if (!target && !_moving && _monitor!.ResynchronizeLateMonitoring)
                                {
                                    _consecutiveLateMonitoringStarts++; _skippedMonitoringTicks++;
                                    if (_consecutiveLateMonitoringStarts >= _monitor.MonitoringConsecutiveLatenessLimit)
                                        throw new TimeoutException($"仅监测连续 {_consecutiveLateMonitoringStarts} 次起始迟到超限；实际迟到={-remaining:F4} ms，容差={limit} ms，已跳过旧拍且停止调度");
                                    ScheduleNextMonitoringInterval();
                                    _phase = "MonitoringResynchronized";
                                    continue; // No late SYNC, no replay and no completed/send counters.
                                }
                                throw new TimeoutException($"周期起始迟到超限，不追赶补发旧周期；实际迟到={-remaining:F4} ms，容差={limit} ms");
                            }
                            if (!target && !_moving) _consecutiveLateMonitoringStarts = 0;
                        }
                        OrderedFrame[] batch;
                        lock (_gate)
                        {
                            _trajectorySelection = null; _trajectorySelectionSubmitted = false;
                            if (target && _trajectoryCursor != null)
                            {
                                _phase = "TrajectorySelection";
                                _trajectorySelection = _trajectoryCursor.Select(_trajectoryClockStarted ? Stopwatch.GetElapsedTime(_motionStart).TotalMilliseconds : 0);
                                if (_trajectorySelection == null)
                                { _nextTarget = _motionStart + Ticks(_trajectoryCursor.NextDueMs); continue; }
                                _targets = _trajectorySelection.Frames;
                            }
                            else if (target && _trajectory != null)
                            {
                                if (_pointIndex >= _trajectory.Points.Length) { _stopMotion = true; continue; }
                                _targets = _trajectory.Points[_pointIndex].Frames;
                            }
                            batch = target ? _targets.Append(_sync!).ToArray() : [_monitorSync!];
                            _phase = target ? "RPDO/SYNC" : "MonitoringSYNC";
                        }
                        var actualStart = Stopwatch.GetTimestamp();
                        if (!target)
                        {
                            lock (_gate)
                            {
                                // Only actual host batch starts contribute to
                                // this interval. Skips/resync do not create one.
                                _lastMonitoringStartIntervalMs = _lastBackgroundStartedAt is long previous
                                    ? Stopwatch.GetElapsedTime(previous, actualStart).TotalMilliseconds : null;
                                _lastBackgroundStartedAt = actualStart;
                            }
                        }
                        try { await SendAsync(batch, target, ct).ConfigureAwait(false); }
                        catch (CanBatchWithdrawnException) { continue; } // No member entered SDK; process the captured stop next.
                        lock (_gate)
                        {
                            if (target && _trajectoryCursor != null)
                            {
                                if (!_trajectorySelectionSubmitted) throw new InvalidOperationException("轨迹批次没有完整提交证据");
                                _nextTarget = _motionStart + Ticks(_trajectoryCursor.NextDueMs);
                            }
                            else if (target && _trajectory != null)
                            {
                                _pointIndex++;
                                _nextTarget = _motionStart + Ticks(_pointIndex < _trajectory.Points.Length
                                    ? _trajectory.Points[_pointIndex].OffsetMs : _trajectory.DurationMs);
                                if (_pointIndex < _trajectory.Points.Length && _nextTarget <= Stopwatch.GetTimestamp())
                                    throw new TimeoutException("下一个轨迹时间点已经错过，禁止密集补发");
                            }
                            else if (target) _nextTarget = actualStart + Ticks(options.PeriodMs);
                            // A target's extra SYNC is not a background tick.
                            // Preserve background phase even when both are due.
                            if (!target && _monitor != null) ScheduleNextMonitoringInterval(actualStart);
                            _phase = null;
                        }
                        delay = 0;
                    }
                }
                finally { _tickGate.Release(); }
                if (delay > 0)
                {
                    lock (_gate) _monitoringDelayPending = !_moving && _monitor != null;
                    try
                    {
                        try { await scheduleWake.WaitAsync(TimeSpan.FromMilliseconds(delay), ct).ConfigureAwait(false); }
                        catch (TimeoutException) { } // Configured relative delay elapsed normally.
                    }
                    finally { lock (_gate) _monitoringDelayPending = false; }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_gate) { _faulted = true; _state = "Faulted"; _error ??= ex.ToString(); _overruns++; _stopMotion = true; }
            if (_moving && !_stopCommandAttempted && !_partialCycle && _submitted == _completedSubmissions && _connection.IsolationReason == null)
            {
                try { await CompleteMotionAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception stopError) { lock (_gate) _error += "\n停机失败：" + stopError; }
            }
        }
        finally
        {
            lock (_gate)
            {
                if (_state == "Running") _state = "HostExited";
                if (_faulted) _state = "Faulted";
                // Worker exit without CompleteMotionAsync must preserve partial/fault results.
                PublishMotionStop(!_faulted && !_partialCycle && _submitted == _completedSubmissions &&
                    (!_moving || (_controlledStop == null && _monitor == null)));
            }
        }
    }

    private void CheckSupervision()
    {
        lock (_gate)
        {
            if (_connection.IsClosing) { _stopMotion = true; return; }
            if (_trajectory == null && Stopwatch.GetElapsedTime(_publishedAt).TotalMilliseconds > _options.InputMaxAgeMs)
                throw new TimeoutException("目标输入已过期（实时目标断供），停止目标发布");
            if (_options.SuperviseFeedback && Stopwatch.GetElapsedTime(_motionStart).TotalMilliseconds >= _options.FeedbackStartupGraceMs)
            {
                var feedback = _connection.Snapshot(_subscription, true);
                // Running includes the connection lifecycle; planned closing is not a data failure.
                if (feedback.BackgroundError != null || feedback.Frames.Any(f => !f.Valid))
                {
                    if (_connection.IsClosing) { _stopMotion = true; return; }
                    throw new TimeoutException("反馈失效或后台接收故障，停止目标发布");
                }
            }
        }
    }

    private void ScheduleNextMonitoringInterval(long? startedAt = null)
    {
        // Called under the service gate. An ordinary background tick uses its
        // own start, not completion of a target batch. A skipped late tick
        // resynchronizes from now; it never catches up old SYNC frames.
        _monitorDelayStartedAt = startedAt ?? Stopwatch.GetTimestamp();
        _nextSync = _monitorDelayStartedAt.Value + Ticks(_monitor!.PeriodMs);
    }
    private void WakeSchedule()
    {
        lock (_gate)
        {
            var previous = _scheduleChanged;
            _scheduleChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
            previous.TrySetResult();
        }
    }

    internal sealed class BatchProgress(CanopenCyclicService service, int length, bool target)
    {
        private int currentSubmitted, currentConfirmed, currentCompleted;
        internal void Report(CanBatchProgress progress)
        {
            lock (service._gate)
            {
                if (progress.Kind == CanBatchProgressKind.FrameConfirmed)
                {
                    service._confirmed += progress.Count; currentConfirmed += progress.Count; service._completedSubmissions += progress.Count; currentCompleted += progress.Count;
                    // A fully evidenced batch is not partial even when its
                    // result arrives late. This does not make the step successful.
                    if (target && currentConfirmed == length && currentSubmitted == length) service._partialCycle = false;
                }
                else if (progress.Kind == CanBatchProgressKind.SubmissionCompleted)
                {
                    service._completedSubmissions += progress.Count; currentCompleted += progress.Count;
                    if (target && currentCompleted == length && currentSubmitted == length) service._partialCycle = false;
                }
                else if (progress.Kind == CanBatchProgressKind.NativeNotEntered)
                {
                    service._submitted -= progress.Count; // Proven no native invocation, never used after submission.
                    currentSubmitted -= progress.Count;
                    if (target && (currentSubmitted == 0 || (currentSubmitted == length && currentCompleted == length))) service._partialCycle = false;
                }
                else { service._submitted += progress.Count; currentSubmitted += progress.Count; if (target) service._partialCycle = true; }
                if (target && currentCompleted == length && currentSubmitted == length &&
                    service._trajectoryCursor != null && service._trajectorySelection != null && !service._trajectorySelectionSubmitted)
                {
                    service._trajectoryCursor.Submitted(service._trajectorySelection); service._trajectorySelectionSubmitted = true;
                    service._pointIndex = service._trajectoryCursor.ExecutedPoints;
                }
                if (target && service._trajectoryCursor == null && service._trajectory != null &&
                    currentCompleted == length && currentSubmitted == length && service._pointIndex + 1 == service._trajectory.Points.Length)
                    service._terminalSubmitted = true;
            }
        }
    }

    private async Task SendAsync(OrderedFrame[] batch, bool target, CancellationToken ct)
    {
        var timeout = target ? _options.CycleTimeoutMs : _monitor!.SendTimeoutMs;
        var period = target ? _options.PeriodMs : _monitor!.PeriodMs;
        var maxStartLateness = target ? _options.MaxLatenessMs : _monitor!.MaxLatenessMs;
        var phase = target ? "RPDO/SYNC" : "MonitoringSYNC";
        var confirmed = target ? _options.RequireOnBusConfirmation : _monitor!.RequireOnBusConfirmation;
        var budget = CanConfirmationBudget.Start(timeout, ct) with { Operation = confirmed ? "发送确认" : "发送提交" };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(timeout);
        var elapsed = Stopwatch.StartNew();
        long queuedAt = Stopwatch.GetTimestamp(), enteredAt = 0, exitedAt = 0;
        bool deadlineObservedWithPendingSend = false;
        var progressState = new BatchProgress(this, batch.Length, target);
        CanBatchDiagnostic? batchDiagnostic = null;
        CancellationToken queueWithdrawal;
        lock (_gate) queueWithdrawal = target ? _targetQueueStop!.Token : default;
        int batchAdmitted = 0;
        lock (_gate) { _sendTaskInFlight = true; _lastTiming = new(phase, period, timeout, maxStartLateness) { SendTaskExited = false, RequireOnBusConfirmation = confirmed }; }
        var sending = Task.Run(() =>
        {
            Volatile.Write(ref enteredAt, Stopwatch.GetTimestamp());
            try
            {
                _connection.SendBatch(this, batch, confirmed, deadline.Token, progressState.Report, budget, value =>
                {
                    lock (_gate)
                    {
                        batchDiagnostic = value;
                        if (_lastTiming != null) _lastTiming = _lastTiming with { Batch = value };
                    }
                }, target ? CanBatchPurpose.Target : CanBatchPurpose.Monitoring, queueWithdrawal, () =>
                {
                    lock (_gate)
                    {
                        if (_closed || _closing || (target && (!_moving || _stopMotion)))
                            throw new CanBatchWithdrawnException();
                        if (target && _trajectoryCursor != null)
                        {
                            // Start the finite table only when its first complete
                            // batch gets a FIFO turn; later turns reselect at the
                            // current monotonic time, never at dequeue-as-capture time.
                            if (!_trajectoryClockStarted) { _motionStart = Stopwatch.GetTimestamp(); _trajectoryClockStarted = true; }
                            _phase = "TrajectoryAdmission";
                            _trajectorySelection = _trajectoryCursor.Select(Stopwatch.GetElapsedTime(_motionStart).TotalMilliseconds)
                                ?? throw new InvalidOperationException("轨迹批次准入时没有当前应发点");
                            _targets = _trajectorySelection.Frames;
                            batch = _targets.Append(_sync!).ToArray();
                            _phase = "RPDO/SYNC";
                        }
                        else if (target && _trajectory != null)
                        {
                            double now = Stopwatch.GetElapsedTime(_motionStart).TotalMilliseconds;
                            if (now >= _trajectory.DurationMs || now - _trajectory.Points[_pointIndex].OffsetMs > _options.MaxLatenessMs)
                                throw new TimeoutException("轨迹点在整批FIFO排队期间超过执行期限，未提交此批");
                        }
                        Volatile.Write(ref batchAdmitted, 1); // Stop linearizes before or after this whole-batch admission.
                        return batch;
                    }
                });
            }
            catch (OperationCanceledException) when (queueWithdrawal.IsCancellationRequested &&
                !deadline.IsCancellationRequested && Volatile.Read(ref batchAdmitted) == 0)
            { throw new CanBatchWithdrawnException(); }
            finally { Volatile.Write(ref exitedAt, Stopwatch.GetTimestamp()); }
        });
        try { await sending.WaitAsync(deadline.Token).ConfigureAwait(false); _ = budget.Remaining(ct); }
        catch (CanBatchWithdrawnException) { throw; }
        catch (Exception waitError)
        {
            if (!sending.IsCompleted && !ct.IsCancellationRequested)
            {
                deadlineObservedWithPendingSend = true;
                lock (_gate) if (_lastTiming != null)
                {
                    _stopMotion = true;
                    _lastTiming = _lastTiming with { DeadlineObservedWithPendingSendTask = true };
                }
            }
            try { await sending.ConfigureAwait(false); }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
            catch (Exception late) { lock (_gate) { _faulted = true; _error = late.ToString(); } }
            if (waitError is OperationCanceledException && deadline.IsCancellationRequested && !ct.IsCancellationRequested)
                throw new TimeoutException($"{(confirmed ? "发送确认" : "发送提交")}超过配置总期限 {timeout} ms；发送任务已实际退出，历史事实见 Timing/Batch", waitError);
            throw;
        }
        finally
        {
            long returnedAt = Stopwatch.GetTimestamp(), entered = Volatile.Read(ref enteredAt), exited = Volatile.Read(ref exitedAt);
            double Milliseconds(long from, long to) => (to - from) * 1000d / Stopwatch.Frequency;
            lock (_gate)
            {
                _sendTaskInFlight = false; // The catch path joins the actual task, including late faults.
                _lastTiming = new(phase, period, timeout, maxStartLateness,
                elapsed.Elapsed.TotalMilliseconds,
                entered == 0 ? null : Milliseconds(queuedAt, entered),
                entered == 0 || exited == 0 ? null : Milliseconds(entered, exited),
                exited == 0 ? null : Milliseconds(exited, returnedAt))
                { Batch = batchDiagnostic, SendTaskExited = true, DeadlineObservedWithPendingSendTask = deadlineObservedWithPendingSend, RequireOnBusConfirmation = confirmed };
            }
        }
        lock (_gate)
        {
            if (target) _partialCycle = false;
            _lastDuration = elapsed.Elapsed.TotalMilliseconds; _cycles++;
            if (!target && _lastDuration > period) _slowMonitoringBatches++;
            // Monitoring cadence is not a second transmission timeout. The
            // configured SendTimeout and actual confirmation budget remain strict.
            // Keep the existing motion-period guard and finite trajectory policy.
            if (target && confirmed && _lastDuration > period)
                throw new TimeoutException($"发送周期执行超限：阶段={phase}，耗时={_lastDuration:F4} ms，目标周期={period} ms，发送确认期限={timeout} ms");
        }
    }

    private async Task CompleteMotionAsync(CancellationToken ct)
    {
        Task queueCancellation;
        lock (_gate) queueCancellation = _targetQueueCancellation;
        await queueCancellation.ConfigureAwait(false);
        lock (_gate)
        {
            _state = "Stopping"; _stopMotion = true;
            if (_partialCycle || _submitted != _completedSubmissions) throw new InvalidOperationException("PartialCycle/提交调用尚未成功结束，禁止恢复监测 SYNC");
        }
        if (_controlledStop != null)
        {
            lock (_gate) _stopCommandAttempted = true;
            var budget = CanConfirmationBudget.Start(_controlledStop.TimeoutMs, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(_controlledStop.TimeoutMs);
            await _connection.RunOwnerCommandAsync(this, async () =>
            {
                foreach (var command in _controlledStop.Commands)
                    await _nodes[command.NodeId].WriteObjectAsync(command.Index, command.SubIndex, command.Data.ToArray(), timeout.Token).ConfigureAwait(false);
            }, budget, timeout.Token, _options.RequireOnBusConfirmation).ConfigureAwait(false);
            lock (_gate) _stopAcknowledged = true;
        }
        lock (_gate)
        {
            ReleaseNodes(); _moving = false; _stopMotion = false; _trajectory = null;
            _state = _faulted ? "FaultedStopped" : _monitor != null ? "Monitoring" : _submitted == _confirmed ? "StoppedConfirmed" : "StoppedHostSubmission";
            // Controlled stop intentionally pauses new cycle submissions.
            // Returning to monitoring keeps its due time; normal lateness
            // handling decides whether to send or resynchronize that tick.
            _consecutiveLateMonitoringStarts = 0;
            // Capture before another monitoring tick can submit a frame. This result belongs
            // to this motion, while the service counters continue to include Monitoring SYNC.
            PublishMotionStop(!_faulted);
        }
    }
    private void PublishMotionStop(bool stopped)
    {
        _motionStopped.TrySetResult(new(_motionGeneration, stopped, _partialCycle,
            _submitted == _completedSubmissions, _stopAcknowledged,
            stopped ? null : _error ?? "本次运动未确认停止，任务已退出"));
    }
    private void ReleaseNodes() { foreach (var node in _nodes.Values) node.Dispose(); _nodes.Clear(); }

    internal void RequestStop()
    {
        lock (_gate)
        {
            if (_stop == null || _stop.IsCancellationRequested) return;
            _generation++; if (!_faulted) _state = "Stopping"; _stopMotion = true; WithdrawQueuedTargets(); _cancelCallbacks = _stop.CancelAsync();
        }
    }
    private void WithdrawQueuedTargets()
    {
        // Called under _gate; callbacks run outside this lock. This token only
        // removes waiting batches and is never passed to an admitted SDK batch.
        if (_targetQueueStop is { IsCancellationRequested: false }) _targetQueueCancellation = _targetQueueStop.CancelAsync();
    }
    internal void FreezeForClose()
    {
        lock (_gate)
        {
            _closing = true;
            if (_moving) { _stopMotion = true; WithdrawQueuedTargets(); WakeSchedule(); }
            else RequestStop();
        }
    }
    internal void CheckSending()
    { lock (_gate) if (_stop?.IsCancellationRequested != false) throw new OperationCanceledException("发送已请求停止"); }

    public async Task StopTargetsAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var stopClock = Stopwatch.StartNew();
        TimeSpan RemainingStop() => TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs - stopClock.Elapsed.TotalMilliseconds));
        Task worker, wait; TaskCompletionSource<MotionStopResult> motion; long generation; bool monitor; string? capturedFault;
        lock (_gate)
        {
            monitor = _monitor != null; motion = _motionStopped; generation = _motionGeneration;
            capturedFault = _faulted ? _error ?? "服务已故障" : null;
            if (monitor && !_moving && !_faulted) return;
            if (_moving && !_worker.IsCompleted)
            { _stopMotion = true; _state = "Stopping"; WithdrawQueuedTargets(); WakeSchedule(); wait = _motionStopped.Task; }
            else { RequestStop(); wait = Task.WhenAll(_worker, _cancelCallbacks, _targetQueueCancellation); }
            worker = _worker;
        }
        try { await wait.WaitAsync(RemainingStop(), ct).ConfigureAwait(false); }
        catch
        {
            lock (_gate)
            {
                if (!ReferenceEquals(worker, _worker) || !ReferenceEquals(motion, _motionStopped) || generation != _motionGeneration)
                    throw new InvalidOperationException("旧代次停止等待已失效，未修改新代次");
                // A completed motion wins a concurrent cancellation of its waiter. Never
                // isolate an already stopped motion because the next monitor tick is active.
                if (monitor && capturedFault == null && motion.Task.IsCompletedSuccessfully &&
                    motion.Task.Result is { Stopped: true, PartialCycle: false, HostSendSettled: true } result && result.Generation == generation)
                    return;
                _faulted = true; _state = "Isolated"; _error ??= "停止等待超时或取消，任务/控制占用仍保留";
            }
            _connection.Isolate("周期发送停止未确认"); throw;
        }
        if (monitor)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(motion, _motionStopped) || generation != _motionGeneration)
                    throw new InvalidOperationException("旧代次停止确认已失效，未修改新代次");
                if (!motion.Task.IsCompletedSuccessfully)
                    throw new InvalidOperationException(_error ?? "本次运动没有停止结果");
                var result = motion.Task.Result;
                if (capturedFault != null) throw new InvalidOperationException(capturedFault);
                if (result.Generation != generation || !result.Stopped || result.PartialCycle || !result.HostSendSettled)
                    throw new InvalidOperationException(result.Error ?? "本次运动停止未确认");
            }
        }
        else
        {
            await worker.WaitAsync(RemainingStop(), ct).ConfigureAwait(false);
            ConfirmExit(worker);
        }
    }

    private void ConfirmExit(Task expected, bool closingConnection = false)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_worker, expected)) throw new InvalidOperationException("旧代次停止确认已失效");
            if (!expected.IsCompleted || !_cancelCallbacks.IsCompleted || !_targetQueueCancellation.IsCompleted)
                throw new InvalidOperationException("服务真实 worker 或取消回调尚未退出，不能释放占用");
            if (closingConnection && !_connection.IsClosing)
                throw new InvalidOperationException("只有父连接已冻结准入后才能走终态关断路径");
            if (_submitted != _completedSubmissions && !closingConnection)
            { _state = "Isolated"; throw new InvalidOperationException($"本次服务关闭未确认：已提交={_submitted}，已确认={_confirmed}；宿主和回调已退出，但发送结果仍未知。仅提交模式也不能伪造确认，请执行独立 CAN_Close 收口；历史故障见 Cycle.Error/ShutdownHistory"); }
            if (!closingConnection && _partialCycle)
            { _faulted = true; _state = "PartialCycle"; throw new InvalidOperationException(_error = "部分 RPDO 已发送但 SYNC 未确认，保留占用，禁止重启"); }
            _connection.ReleaseSender(this); ReleaseNodes(); _stop?.Dispose(); _stop = null; _moving = false; _monitor = null;
            _targetQueueStop?.Dispose(); _targetQueueStop = null;
            _state = closingConnection && (_submitted != _completedSubmissions || _partialCycle)
                ? "ClosedWithUnconfirmedTransmit" : _faulted ? "FaultedStopped" : _submitted == _confirmed ? "StoppedConfirmed" : "StoppedHostSubmission";
        }
    }

    public async Task CloseAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        lock (_gate) _currentCloseError = null;
        try { await CloseServiceCoreAsync(timeoutMs, ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            lock (_gate) if (!_closed) _currentCloseError = ex.ToString();
            throw;
        }
    }
    private async Task CloseServiceCoreAsync(int timeoutMs, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        var clock = Stopwatch.StartNew();
        lock (_gate) _closing = true;
        Exception? history = null;
        try { await StopTargetsAsync(timeoutMs, ct).ConfigureAwait(false); }
        catch (Exception ex) when (!ct.IsCancellationRequested && _worker.IsCompleted)
        {
            history = ex;
            lock (_gate) _closeHistory = ex;
        }
        RequestStop();
        int remaining = timeoutMs - (int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds);
        if (remaining <= 0) throw new TimeoutException("CANopen 服务关闭总期限已到，真实退出尚待确认");
        await Task.WhenAll(_worker, _cancelCallbacks, _targetQueueCancellation).WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false);
        ConfirmExit(_worker); _subscription.Dispose(); lock (_gate) { _closed = true; if (history != null) _closeHistory = history; }
        if (history != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(history).Throw();
    }
    internal async Task CloseForConnectionAsync()
    {
        bool finishMotion;
        int stopTimeout;
        lock (_gate)
        {
            _closing = true;
            finishMotion = _moving && !_faulted && !_stop!.IsCancellationRequested;
            stopTimeout = _controlledStop?.TimeoutMs ?? _options.CycleTimeoutMs;
        }
        // The outer connection close has its own soft wait; it must not replace
        // the configured owned-stop budget with StopTargetsAsync's legacy default.
        var failures = new List<Exception>();
        try { if (finishMotion) await StopTargetsAsync(stopTimeout).ConfigureAwait(false); }
        catch (Exception ex) { failures.Add(ex); }
        // A failed business stop must not suppress parent-connection teardown.
        // Join real workers before releasing their managed claims or SDK handles.
        RequestStop();
        try { await Task.WhenAll(_worker, _cancelCallbacks, _targetQueueCancellation).ConfigureAwait(false); }
        catch (Exception ex) { failures.Add(ex); }
        lock (_gate)
        {
            if (_submitted != _completedSubmissions || _partialCycle)
                _error ??= "父连接终态关闭：部分周期或发送确认仍未知；不恢复目标/SYNC，不代表报文撤回或电机停稳";
        }
        Exception? history = failures.Count switch
        {
            0 => null,
            1 => failures[0],
            _ => new AggregateException("服务工作单元已退出，但父连接关断仍保留停机/取消历史错误", failures)
        };
        ConfirmExit(_worker, true); _subscription.Dispose();
        lock (_gate)
        {
            _closed = true;
            if (history != null) _closeHistory = history;
            else if (_currentCloseError != null) _closeHistory ??= new InvalidOperationException("此前服务关闭未完成：" + _currentCloseError);
            _currentCloseError = null;
        }
        if (history != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(history).Throw();
    }
    public void Dispose()
    {
        var close = CloseAsync();
        _ = close.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
