using System.Diagnostics;
using CAN.CANopen;
using PluginCommon;

namespace CAN.Runtime;

/// <summary>单连接一个 SYNC 调度者；序列输入在启动/发布时复制，后台不求值。</summary>
public sealed class CanopenCyclicService : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private readonly CanConnectionResource _connection;
    private readonly FeedbackSubscription _subscription;
    private readonly Dictionary<byte, CanopenNodeAccess> _nodes = [];
    private CancellationTokenSource? _stop;
    private Task _worker = Task.CompletedTask, _cancelCallbacks = Task.CompletedTask;
    private sealed record MotionStopResult(long Generation, bool Stopped, bool PartialCycle,
        bool TransmitConfirmed, bool StopCommandsAcknowledged, string? Error);
    private TaskCompletionSource<MotionStopResult> _motionStopped = MotionSignal();
    private OrderedFrame[] _targets = [];
    private OrderedFrame? _sync;
    private MonitoringOptions? _monitor;
    private CycleOptions _options = new();
    private ControlledStopOptions? _controlledStop;
    private CanTrajectory? _trajectory;
    private long _generation, _motionGeneration, _version, _publishedAt, _motionStart, _nextTarget, _nextSync;
    private long _cycles, _submitted, _confirmed, _overruns;
    private string _state = "Idle";
    private string? _phase, _error;
    private double _lastDuration;
    private long _slowMonitoringBatches;
    private CycleTimingStatus? _lastTiming;
    private Exception? _closeHistory;
    internal Exception? CloseHistory { get { lock (_gate) return _closeHistory; } }
    private bool _closed, _closing, _faulted, _moving, _stopMotion, _partialCycle;
    private bool _stopAcknowledged, _stopCommandAttempted;
    private int _pointIndex;
    internal bool IsClosed { get { lock (_gate) return _closed; } }

    private static TaskCompletionSource<MotionStopResult> MotionSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static long Ticks(int ms) => checked((long)(ms * (Stopwatch.Frequency / 1000d)));
    internal CanopenCyclicService(CanConnectionResource connection, FeedbackSubscription subscription)
        => (_connection, _subscription) = (connection, subscription);

    public CommunicationSnapshot Snapshot()
    {
        CycleStatus cycle; bool running; string? closeHistory;
        bool connectionClosed = _connection.IsClosed;
        lock (_gate)
        {
            running = !_closed;
            closeHistory = _closeHistory?.ToString();
            cycle = new(_state, _generation, _version, _cycles, _submitted, _confirmed, _lastDuration,
                _overruns, _phase, _error, _worker.IsCompleted && _cancelCallbacks.IsCompleted,
                _submitted == _confirmed || connectionClosed ? true : null)
            { MotionActive = _moving, MonitoringSyncActive = _monitor != null && !_worker.IsCompleted && !_faulted,
                StopCommandsAcknowledged = _stopAcknowledged, ExecutedTrajectoryPoints = _pointIndex,
                SlowMonitoringBatches = _slowMonitoringBatches, Timing = _lastTiming,
                ServiceClosed = _closed, PartialCycle = _partialCycle, ConnectionClosed = connectionClosed };
        }
        return new(_connection.Snapshot(_subscription, running), cycle) { ShutdownHistory = closeHistory };
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
    private void CheckConfirmation(bool require, int timeoutMs, CancellationToken ct = default)
    {
        if (!require) return;
        if (!_connection.SupportsConfirmedTransmit)
            throw new NotSupportedException("UnsupportedGuarantee：适配器不支持总线发送确认，未发送目标");
        _connection.VerifyConfirmationAvailable(timeoutMs, ct);
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
        if (options.PeriodMs <= 0 || options.SendTimeoutMs <= 0 || options.MaxLatenessMs < 0)
            throw new ArgumentException("监测周期参数无效");
        CheckConfirmation(true, options.SendTimeoutMs, ct);
        lock (_gate)
        {
            ct.ThrowIfCancellationRequested();
            if (budget.ElapsedMilliseconds >= options.SendTimeoutMs) throw new TimeoutException("监测启动总期限已到，未发布 SYNC");
            CheckStart();
            if (_monitor != null) throw new InvalidOperationException("监测已启动");
            _connection.ReserveSender(this, [sync.CobId]);
            _sync = sync with { Data = sync.Data.ToArray() }; _monitor = options;
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
            nodes.Length == 0 || nodes.Any(n => n is < 1 or > 127) || nodes.Distinct().Count() != nodes.Length)
            throw new ArgumentException("节点、周期、有效期或超时配置无效");
        lock (_gate) CheckStart();
        CheckConfirmation(options.RequireOnBusConfirmation, Remaining(), ct);
        if (!_tickGate.Wait(Remaining(), ct)) throw new TimeoutException("等待监测周期退出超时，未发布目标");
        try
        {
            lock (_gate)
            {
                CheckStart();
                _ = Remaining();
                var selectedStop = replaceStop ? stopCopy : _controlledStop;
                if (_monitor != null && selectedStop == null)
                    throw new InvalidOperationException("监测 SYNC 下启动运动必须先配置经设备审查的停机命令，不能只停止目标后继续激活旧目标");
                if (_monitor != null && (!options.RequireOnBusConfirmation || _sync!.CobId != sync.CobId || !_sync.Data.SequenceEqual(sync.Data)))
                    throw new ArgumentException("运动必须复用监测的 SYNC 配置及实际发送确认");
                if (selectedStop?.Commands.Any(c => !nodes.Contains(c.NodeId)) == true)
                    throw new ArgumentException("停机命令只能访问当前控制所有者占用的节点");
                long ownedGeneration = _generation + 1;
                ExecutionScopedExecutor.Own(() => RetireMotion(ownedGeneration));
                try
                {
                    foreach (var node in nodes)
                        _nodes.Add(node, CanopenConnectionAccess.For(_connection).ReserveNode(node, Remaining()));
                    _ = Remaining();
                    var ids = copy.Select(f => f.CobId).Append(sync.CobId).ToArray();
                    if (_monitor == null) _connection.ReserveSender(this, ids);
                    else _connection.UpdateSender(this, ids);
                }
                catch { ReleaseNodes(); throw; }
                _controlledStop = selectedStop;
                _targets = copy; _sync = sync with { Data = sync.Data.ToArray() }; _options = options; _trajectory = trajectory;
                _pointIndex = 0;
                _stopCommandAttempted = false;
                _moving = true; _stopMotion = false; _motionStopped = MotionSignal(); _partialCycle = false; _stopAcknowledged = false;
                _motionStart = _publishedAt = _nextTarget = Stopwatch.GetTimestamp();
                _version++; _generation++; _motionGeneration = _generation; _state = "Running"; _error = _phase = null;
                if (_monitor == null) { _cycles = _submitted = _confirmed = _overruns = 0; StartWorker(); }
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
            if (_moving && (_controlledStop != null || _monitor != null)) { _stopMotion = true; _state = "Stopping"; }
            else RequestStop();
        }
    }
    private void RetireMotion(long generation)
    {
        lock (_gate)
        {
            if (!_moving || _motionGeneration != generation) return;
            _stopMotion = true; _state = "Stopping";
            if (_monitor == null && _controlledStop == null) RequestStop();
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
                try
                {
                    bool stopMotion, target; long due; CycleOptions options;
                    lock (_gate)
                    {
                        options = _options;
                        if (_moving && _trajectory != null && Stopwatch.GetElapsedTime(_motionStart).TotalMilliseconds >= _trajectory.DurationMs)
                        {
                            if (_pointIndex < _trajectory.Points.Length)
                                throw new TimeoutException("轨迹结束期限已到，但存在未执行时间点；禁止跳点或补发");
                            _stopMotion = true;
                        }
                        stopMotion = _moving && _stopMotion;
                        target = _moving && (_monitor == null || _nextTarget <= _nextSync);
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
                    var remaining = (due - Stopwatch.GetTimestamp()) * 1000d / Stopwatch.Frequency;
                    if (remaining > 0) delay = Math.Min(remaining, 5);
                    else
                    {
                        var limit = target ? options.MaxLatenessMs : _monitor!.MaxLatenessMs;
                        if (-remaining > limit) throw new TimeoutException("周期起始迟到超限，不追赶补发旧周期");
                        OrderedFrame[] batch;
                        lock (_gate)
                        {
                            if (target && _trajectory != null)
                            {
                                if (_pointIndex >= _trajectory.Points.Length) { _stopMotion = true; continue; }
                                _targets = _trajectory.Points[_pointIndex].Frames;
                            }
                            batch = target ? _targets.Append(_sync!).ToArray() : [_sync!];
                            _phase = target ? "RPDO/SYNC" : "MonitoringSYNC";
                        }
                        var actualStart = Stopwatch.GetTimestamp();
                        await SendAsync(batch, target, ct).ConfigureAwait(false);
                        lock (_gate)
                        {
                            if (target && _trajectory != null)
                            {
                                _pointIndex++;
                                _nextTarget = _motionStart + Ticks(_pointIndex < _trajectory.Points.Length
                                    ? _trajectory.Points[_pointIndex].OffsetMs : _trajectory.DurationMs);
                                if (_pointIndex < _trajectory.Points.Length && _nextTarget <= Stopwatch.GetTimestamp())
                                    throw new TimeoutException("下一个轨迹时间点已经错过，禁止密集补发");
                            }
                            else if (target) _nextTarget = actualStart + Ticks(options.PeriodMs);
                            if (_monitor != null) _nextSync = Stopwatch.GetTimestamp() + Ticks(_monitor.PeriodMs);
                            _phase = null;
                        }
                        delay = 0;
                    }
                }
                finally { _tickGate.Release(); }
                if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_gate) { _faulted = true; _state = "Faulted"; _error ??= ex.ToString(); _overruns++; _stopMotion = true; }
            if (_moving && !_stopCommandAttempted && !_partialCycle && _submitted == _confirmed && _connection.IsolationReason == null)
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
                PublishMotionStop(!_faulted && !_partialCycle && _submitted == _confirmed &&
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

    private async Task SendAsync(OrderedFrame[] batch, bool target, CancellationToken ct)
    {
        var timeout = target ? _options.CycleTimeoutMs : _monitor!.SendTimeoutMs;
        var period = target ? _options.PeriodMs : _monitor!.PeriodMs;
        var maxStartLateness = target ? _options.MaxLatenessMs : _monitor!.MaxLatenessMs;
        var phase = target ? "RPDO/SYNC" : "MonitoringSYNC";
        var confirmed = !target || _options.RequireOnBusConfirmation;
        var budget = CanConfirmationBudget.Start(timeout, ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(timeout);
        var elapsed = Stopwatch.StartNew();
        long queuedAt = Stopwatch.GetTimestamp(), enteredAt = 0, exitedAt = 0;
        lock (_gate) _lastTiming = new(phase, period, timeout, maxStartLateness);
        var sending = Task.Run(() =>
        {
            Volatile.Write(ref enteredAt, Stopwatch.GetTimestamp());
            try
            {
                _connection.SendBatch(this, batch, confirmed, deadline.Token, done =>
                {
                    lock (_gate) { if (done) _confirmed++; else { _submitted++; if (target) _partialCycle = true; } }
                }, budget);
            }
            finally { Volatile.Write(ref exitedAt, Stopwatch.GetTimestamp()); }
        });
        try { await sending.WaitAsync(deadline.Token).ConfigureAwait(false); }
        catch
        {
            if (!sending.IsCompleted && !ct.IsCancellationRequested) _connection.Isolate("周期原生发送超过期限，实际调用尚未退出");
            try { await sending.ConfigureAwait(false); }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
            catch (Exception late) { lock (_gate) { _faulted = true; _error = late.ToString(); } }
            throw;
        }
        finally
        {
            long returnedAt = Stopwatch.GetTimestamp(), entered = Volatile.Read(ref enteredAt), exited = Volatile.Read(ref exitedAt);
            double Milliseconds(long from, long to) => (to - from) * 1000d / Stopwatch.Frequency;
            lock (_gate) _lastTiming = new(phase, period, timeout, maxStartLateness,
                elapsed.Elapsed.TotalMilliseconds,
                entered == 0 ? null : Milliseconds(queuedAt, entered),
                entered == 0 || exited == 0 ? null : Milliseconds(entered, exited),
                exited == 0 ? null : Milliseconds(exited, returnedAt));
        }
        lock (_gate)
        {
            if (target) _partialCycle = false;
            _lastDuration = elapsed.Elapsed.TotalMilliseconds; _cycles++;
            if (!target && _lastDuration > period) _slowMonitoringBatches++;
            // Monitoring cadence is not a second transmission timeout. The
            // configured SendTimeout and actual confirmation budget remain strict.
            // Keep the existing motion-period guard and finite trajectory policy.
            if (target && _lastDuration > period)
                throw new TimeoutException($"发送周期执行超限：阶段={phase}，耗时={_lastDuration:F4} ms，目标周期={period} ms，发送确认期限={timeout} ms");
        }
    }

    private async Task CompleteMotionAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            _state = "Stopping"; _stopMotion = true;
            if (_partialCycle || _submitted != _confirmed) throw new InvalidOperationException("PartialCycle/待发未知，禁止恢复监测 SYNC");
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
            }, budget, timeout.Token).ConfigureAwait(false);
            lock (_gate) _stopAcknowledged = true;
        }
        lock (_gate)
        {
            ReleaseNodes(); _moving = false; _stopMotion = false; _trajectory = null;
            _state = _faulted ? "FaultedStopped" : _monitor != null ? "Monitoring" : "StoppedConfirmed";
            _nextSync = Stopwatch.GetTimestamp() + Ticks(_monitor?.PeriodMs ?? 1);
            // Capture before another monitoring tick can submit a frame. This result belongs
            // to this motion, while the service counters continue to include Monitoring SYNC.
            PublishMotionStop(!_faulted);
        }
    }
    private void PublishMotionStop(bool stopped)
    {
        _motionStopped.TrySetResult(new(_motionGeneration, stopped, _partialCycle,
            _submitted == _confirmed, _stopAcknowledged,
            stopped ? null : _error ?? "本次运动未确认停止，任务已退出"));
    }
    private void ReleaseNodes() { foreach (var node in _nodes.Values) node.Dispose(); _nodes.Clear(); }

    internal void RequestStop()
    {
        lock (_gate)
        {
            if (_stop == null || _stop.IsCancellationRequested) return;
            _generation++; _state = "Stopping"; _stopMotion = true; _cancelCallbacks = _stop.CancelAsync();
        }
    }
    internal void FreezeForClose()
    {
        lock (_gate)
        {
            _closing = true;
            if (_moving && (_monitor != null || _controlledStop != null)) _stopMotion = true;
            else RequestStop();
        }
    }
    internal void CheckSending()
    { lock (_gate) if (_stop?.IsCancellationRequested != false) throw new OperationCanceledException("发送已请求停止"); }

    public async Task StopTargetsAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        Task worker, wait; TaskCompletionSource<MotionStopResult> motion; long generation; bool monitor; string? capturedFault;
        lock (_gate)
        {
            monitor = _monitor != null; motion = _motionStopped; generation = _motionGeneration;
            capturedFault = _faulted ? _error ?? "服务已故障" : null;
            if (monitor && !_moving && !_faulted) return;
            if ((_monitor != null || _controlledStop != null) && !_worker.IsCompleted)
            { _stopMotion = true; _state = "Stopping"; wait = _motionStopped.Task; }
            else { RequestStop(); wait = Task.WhenAll(_worker, _cancelCallbacks); }
            worker = _worker;
        }
        try { await wait.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
        catch
        {
            lock (_gate)
            {
                if (!ReferenceEquals(worker, _worker) || !ReferenceEquals(motion, _motionStopped) || generation != _motionGeneration)
                    throw new InvalidOperationException("旧代次停止等待已失效，未修改新代次");
                // A completed motion wins a concurrent cancellation of its waiter. Never
                // isolate an already stopped motion because the next monitor tick is active.
                if (monitor && capturedFault == null && motion.Task.IsCompletedSuccessfully &&
                    motion.Task.Result is { Stopped: true, PartialCycle: false, TransmitConfirmed: true } result && result.Generation == generation)
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
                if (result.Generation != generation || !result.Stopped || result.PartialCycle || !result.TransmitConfirmed)
                    throw new InvalidOperationException(result.Error ?? "本次运动停止未确认");
            }
        }
        else
        {
            await worker.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
            ConfirmExit(worker);
        }
    }

    private void ConfirmExit(Task expected, bool closingConnection = false)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_worker, expected)) throw new InvalidOperationException("旧代次停止确认已失效");
            if (!expected.IsCompleted || !_cancelCallbacks.IsCompleted)
                throw new InvalidOperationException("服务真实 worker 或取消回调尚未退出，不能释放占用");
            if (closingConnection && !_connection.IsClosing)
                throw new InvalidOperationException("只有父连接已冻结准入后才能走终态关断路径");
            if (_submitted != _confirmed && !closingConnection)
            { _state = "Isolated"; throw new InvalidOperationException(_error ??= "旧帧发送状态未知，不能确认停止"); }
            if (!closingConnection && _partialCycle)
            { _faulted = true; _state = "PartialCycle"; throw new InvalidOperationException(_error = "部分 RPDO 已发送但 SYNC 未确认，保留占用，禁止重启"); }
            _connection.ReleaseSender(this); ReleaseNodes(); _stop?.Dispose(); _stop = null; _moving = false; _monitor = null;
            _state = closingConnection && (_submitted != _confirmed || _partialCycle)
                ? "ClosedWithUnconfirmedTransmit" : _faulted ? "FaultedStopped" : "StoppedConfirmed";
        }
    }

    public async Task CloseAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        var clock = Stopwatch.StartNew();
        lock (_gate) _closing = true;
        Exception? history = null;
        try { await StopTargetsAsync(timeoutMs, ct).ConfigureAwait(false); }
        catch (Exception ex) when (!ct.IsCancellationRequested && _worker.IsCompleted) { history = ex; }
        RequestStop();
        int remaining = timeoutMs - (int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds);
        if (remaining <= 0) throw new TimeoutException("CANopen 服务关闭总期限已到，真实退出尚待确认");
        await Task.WhenAll(_worker, _cancelCallbacks).WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false);
        ConfirmExit(_worker); _subscription.Dispose(); lock (_gate) { _closed = true; _closeHistory = history; }
        if (history != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(history).Throw();
    }
    internal async Task CloseForConnectionAsync()
    {
        bool finishMotion;
        int stopTimeout;
        lock (_gate)
        {
            _closing = true;
            finishMotion = _moving && (_monitor != null || _controlledStop != null) && !_faulted && !_stop!.IsCancellationRequested;
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
        try { await Task.WhenAll(_worker, _cancelCallbacks).ConfigureAwait(false); }
        catch (Exception ex) { failures.Add(ex); }
        lock (_gate)
        {
            if (_submitted != _confirmed || _partialCycle)
                _error ??= "父连接终态关闭：部分周期或发送确认仍未知；不恢复目标/SYNC，不代表报文撤回或电机停稳";
        }
        Exception? history = failures.Count switch
        {
            0 => null,
            1 => failures[0],
            _ => new AggregateException("服务工作单元已退出，但父连接关断仍保留停机/取消历史错误", failures)
        };
        ConfirmExit(_worker, true); _subscription.Dispose();
        lock (_gate) { _closed = true; if (history != null) _closeHistory = history; }
        if (history != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(history).Throw();
    }
    public void Dispose()
    {
        var close = CloseAsync();
        _ = close.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
