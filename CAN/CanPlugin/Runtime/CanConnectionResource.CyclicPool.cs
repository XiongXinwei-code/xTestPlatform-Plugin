using System.Diagnostics;
using CAN.Helpers;
using CAN.Models;

namespace CAN.Runtime;

internal sealed record CanCyclicDefinition(uint Id, CanFrameType FrameType, bool Fd, CanCyclicControl Control,
    int RefreshMs, int TimeoutMs, CanPayloadProtectionSetting Protection,
    Func<CancellationToken, Action, Task<CanMessage>> Build,
    Func<CancellationToken, Action, Task<CanCyclicControl>> Sample,
    Action CheckLifetime, Func<Task> Drain);

internal sealed class CanCyclicChangedException : OperationCanceledException;

/// <summary>连接唯一发送池。按 ID 保留节拍和计数，任务名仅表示当前配置的归属。</summary>
public sealed partial class CanConnectionResource
{
    private const string CyclicPoolJob = "\u001fCAN.CyclicPool";
    private readonly object _cyclicGate = new();
    private readonly Dictionary<(CanFrameType, uint), CyclicSlot> _cyclicSlots = [];
    private readonly List<CanCyclicConfiguration> _cyclicConfigurations = [];
    private long _cyclicGeneration;
    private long _lastCyclicHistoryTrim;
    private CanBackgroundJob? _cyclicPoolJob;

    internal void UpdateCyclicPool(string name, string kind, CanCyclicDefinition[] definitions, Action check)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("周期任务名不能为空");
        if (definitions.Length is < 1 or > 128) throw new ArgumentException("周期发送池需要 1～128 行报文");
        if (definitions.Select(d => (d.FrameType, d.Id)).Distinct().Count() != definitions.Length)
            throw new ArgumentException("同一步骤不允许重复帧类型和 CAN ID（经典帧与 FD 共用同一 ID 槽位）");
        foreach (var definition in definitions) CanDynamicCyclicLoop.Validate(definition.Control);
        lock (_gate)
        {
            check(); CheckLocked();
            if (_senders.Values.Any(c => c.Diagnostic) || _startingSenders.Values.Any(c => c))
                throw new InvalidOperationException("逐批确认诊断正在占用发送连接");
            if (_jobs.ContainsKey(name)) throw new InvalidOperationException($"同名旧通用任务 {name} 仍存在，请先停止");
            lock (_cyclicGate)
            {
                if (_cyclicPoolJob?.Error != null) throw new InvalidOperationException("周期发送池后台故障，请关闭 CAN 后重开：" + _cyclicPoolJob.Error);
                if (_cyclicSlots.Count + definitions.Count(d => !_cyclicSlots.ContainsKey((d.FrameType, d.Id))) > 4096)
                    throw new InvalidOperationException("本连接周期 ID 槽位超过 4096 个，请关闭后重新打开连接");
                // Prepare all replacements before publishing any. Invalid later rows cannot erase earlier owners.
                var processors = definitions.Select(d => _cyclicSlots.TryGetValue((d.FrameType, d.Id), out var slot) && slot.Processor != null && slot.Current is not { Stopped: true }
                    ? slot.Processor.Reconfigure(d.Protection) : new CanPayloadProcessor(d.Protection)).ToArray();
                check();
                long generation = ++_cyclicGeneration;
                for (int i = 0; i < definitions.Length; i++)
                {
                    var definition = definitions[i];
                    var key = (definition.FrameType, definition.Id);
                    if (!_cyclicSlots.TryGetValue(key, out var slot))
                        _cyclicSlots.Add(key, slot = new() { NextDue = AddCyclicPeriod(Stopwatch.GetTimestamp(), definition.Control.PeriodMs) });
                    if (slot.Current is { Stopped: true }) slot.NextDue = AddCyclicPeriod(Stopwatch.GetTimestamp(), definition.Control.PeriodMs);
                    if (slot.Current is { } old) { old.Superseded = true; old.Retire(); }
                    var configuration = new CanCyclicConfiguration(this, slot, name, kind, generation, i + 1, definition, processors[i]);
                    slot.Current = configuration;
                    slot.Processor = processors[i];
                    slot.LatestGeneration = generation;
                    _cyclicConfigurations.Add(configuration);
                    PluginCommon.ExecutionScopedExecutor.Own(() =>
                    { lock (_cyclicGate) { configuration.Stopped = true; configuration.CancelBatches(); _ = configuration.DrainAsync(); } });
                }
                if (_cyclicPoolJob == null)
                {
                    // The pool belongs to the connection, not the first short-lived startup step.
                    using (ExecutionContext.SuppressFlow()) _cyclicPoolJob = StartJob(CyclicPoolJob, RunCyclicPoolAsync);
                }
            }
        }
    }

    private async Task RunCyclicPoolAsync(CancellationToken token)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        token = lifetime.Token;
        // Control refresh must remain independent while a submission waits for the FIFO gate/driver.
        var controls = Task.Run(async () =>
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                CanCyclicConfiguration[] current;
                lock (_cyclicGate)
                {
                    TrimCyclicHistoryLocked();
                    current = _cyclicSlots.Values.Select(s => s.Current).OfType<CanCyclicConfiguration>().Where(c => c.IsUsable).ToArray();
                }
                foreach (var c in current) c.StartControlSample(token);
                await Task.Delay(1, token).ConfigureAwait(false);
            }
        }, CancellationToken.None);
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                CanCyclicConfiguration[] active;
                lock (_cyclicGate) active = _cyclicSlots.Values.Select(s => s.Current).OfType<CanCyclicConfiguration>()
                    .Where(c => c.IsUsable).ToArray();
                var due = active.Where(c => c.IsDue).ToArray();
                foreach (var c in due) c.StartBuild(token);
                var ready = due.Where(c => c.BuildReady).ToArray();
                if (ready.Length == 0) { await Task.Delay(1, token).ConfigureAwait(false); continue; }
                // A common FIFO batch preserves list order for simultaneously due raw and DBC rows.
                foreach (var group in ready.Chunk(128)) await SendCyclicPoolBatchAsync(group, token).ConfigureAwait(false);
            }
        }
        finally
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            try { await controls.ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            CanCyclicConfiguration[] all;
            lock (_cyclicGate) { all = _cyclicConfigurations.ToArray(); foreach (var c in all) c.Stopped = true; }
            await Task.WhenAll(all.Select(c => c.DrainAsync())).ConfigureAwait(false);
        }
    }

    private async Task SendCyclicPoolBatchAsync(CanCyclicConfiguration[] due, CancellationToken token)
    {
        var frames = new List<ProtectedCanFrame>();
        foreach (var c in due)
        {
            try
            {
                c.Check();
                var frame = await c.TakeBuiltAsync().ConfigureAwait(false);
                CanHelper.ValidateTransmitFrame(frame);
                CanPayloadProtection.Validate(c.Definition.Protection, frame.Data.Length);
                if (!c.MoveTo(frame)) continue;
                c.Check();
                frames.Add(new(frame, c.Definition.Protection, $"pool/{c.Generation}/{c.Row}", c.Row, c.Processor, c));
            }
            catch (CanCyclicChangedException) { }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { c.Fail(ex); }
        }
        if (frames.Count == 0) return;
        int timeout = frames.Min(f => f.Cyclic!.Definition.TimeoutMs);
        CanCyclicBatch batch;
        lock (_cyclicGate)
        {
            frames.RemoveAll(frame => !frame.Cyclic!.IsUsable);
            if (frames.Count == 0) return;
            batch = new(this, frames.Select(f => f.Cyclic!).ToArray(), token);
            foreach (var frame in frames) frame.Cyclic!.Batches.Add(batch);
        }
        try { await WriteProtectedBatchAsync(frames.ToArray(), timeout, batch.Token, trackActual: batch.TrackActual, isolateOnCancellation: false).ConfigureAwait(false); }
        catch (CanCyclicChangedException) { } // Never retry a submitted frame; only the next scheduled tick is eligible.
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (batch.Token.IsCancellationRequested) { }
        catch (Exception ex) { foreach (var frame in frames) frame.Cyclic!.Fail(ex); }
        finally
        {
            batch.Finish();
            lock (_cyclicGate)
                foreach (var frame in frames)
                {
                    var c = frame.Cyclic!;
                    // An asynchronous control update may already have scheduled a new deadline.
                    if ((c.AttemptedThisTick || c.IsUsable) && c.Slot.NextDue == c.TickDue)
                        c.Slot.NextDue = AddCyclicPeriod(Stopwatch.GetTimestamp(), c.Slot.Current?.Control.PeriodMs ?? c.Control.PeriodMs);
                    c.AttemptedThisTick = false;
                }
        }
    }

    private static long AddCyclicPeriod(long ticks, int ms) => ticks + (long)(ms * (double)Stopwatch.Frequency / 1000d);

    private void TrimCyclicHistoryLocked()
    {
        if (Stopwatch.GetElapsedTime(_lastCyclicHistoryTrim).TotalMilliseconds < 500) return;
        _lastCyclicHistoryTrim = Stopwatch.GetTimestamp();
        // Retain actual unfinished work, but never keep every old expression context for a long-running update loop.
        var latest = _cyclicConfigurations.GroupBy(c => (c.TaskName, c.Slot)).ToDictionary(g => g.Key, g => g.Max(c => c.Generation));
        _cyclicConfigurations.RemoveAll(c => c.Superseded && c.Retired && latest[(c.TaskName, c.Slot)] > c.Generation);
        var history = _cyclicConfigurations.Where(c => c.Superseded && c.Retired).OrderByDescending(c => c.Generation).Skip(256).ToHashSet();
        if (history.Count != 0) _cyclicConfigurations.RemoveAll(history.Contains);
    }

    internal object[] GetCyclicStatuses()
    {
        lock (_cyclicGate)
            return _cyclicConfigurations.GroupBy(c => c.TaskName, StringComparer.Ordinal).Select(group => (object)new
            {
                TaskName = group.Key,
                Direction = "Transmit",
                Active = group.Any(c => c.IsUsable && c.Control.Enabled),
                Configured = group.Any(c => c.IsUsable),
                Healthy = group.Where(c => !c.Superseded && !c.Stopped).All(c => c.Error == null) && _cyclicPoolJob?.Error == null,
                Error = string.Join(Environment.NewLine, group.Where(c => c.Error != null).Select(c => c.Error).Distinct()),
                Messages = group.Select(c => c.Snapshot()).ToArray()
            }).ToArray();
    }

    private bool HasCyclicTask(string name)
    { lock (_cyclicGate) return _cyclicConfigurations.Any(c => c.TaskName == name); }

    private bool HasLiveCyclicName(string name)
    { lock (_cyclicGate) return _cyclicConfigurations.Any(c => c.TaskName == name && (!c.Stopped && !c.Superseded || c.NativePending)); }

    // Called under the connection gate: listeners are observers, and an idle pool host sends nothing.
    private bool HasSendingJobsLocked()
    {
        var observers = _listenerJobs.Values.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var job in _jobs)
        {
            if (observers.Contains(job.Key)) continue;
            if (job.Key != CyclicPoolJob) return true;
            lock (_cyclicGate)
                if (_cyclicConfigurations.Any(c => !c.Stopped && !c.Superseded || c.HasUnfinishedWork)) return true;
        }
        return false;
    }

    private async Task<string?> StopCyclicTaskAsync(string name, int timeoutMs, CancellationToken ct)
    {
        CanCyclicConfiguration[] selected;
        lock (_cyclicGate)
        {
            selected = _cyclicConfigurations.Where(c => c.TaskName == name).ToArray();
            // DrainAsync caches one task. Register it before releasing the lock so diagnostic admission cannot see a false idle gap.
            foreach (var c in selected) { c.Stopped = true; c.CancelBatches(); _ = c.DrainAsync(); }
        }
        var actual = Task.WhenAll(selected.Select(c => c.DrainAsync()));
        try { await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
        catch
        {
            _ = actual.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            bool pending; lock (_cyclicGate) pending = selected.Any(c => c.NativePending);
            if (pending) Isolate($"周期任务 {name} 停止等待结束，真实原生发送尚未退出");
            throw;
        }
        return string.Join(Environment.NewLine, selected.Where(c => c.Error != null).Select(c => c.Error).Distinct()) is { Length: > 0 } error ? error : null;
    }

    internal sealed class CyclicSlot
    {
        internal CanCyclicConfiguration? Current;
        internal CanPayloadProcessor? Processor;
        internal long NextDue, LatestGeneration;
    }

    internal sealed class CanCyclicBatch(CanConnectionResource owner, CanCyclicConfiguration[] rows, CancellationToken token)
    {
        private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        private Task _actual = Task.CompletedTask;
        private Task _cancellation = Task.CompletedTask;
        private bool _finishing;
        private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Completion => _finished.Task;
        internal CancellationToken Token => _stop.Token;
        internal void TrackActual(Task actual) => _actual = actual;
        internal void Cancel()
        { lock (owner._cyclicGate) if (!_finishing && !_stop.IsCancellationRequested) _cancellation = _stop.CancelAsync(); }
        internal void Finish()
        {
            _ = FinishAsync();
        }
        private async Task FinishAsync()
        {
            await _actual.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).ConfigureAwait(false);
            Task callbacks;
            lock (owner._cyclicGate)
            {
                callbacks = _cancellation;
                // The actual call has exited. Freeze the callback set while retaining the batch until every callback exits.
                _finishing = true;
            }
            try { await callbacks.ConfigureAwait(false); }
            catch (Exception ex) { lock (owner._cyclicGate) foreach (var row in rows) row.Error ??= "周期取消回调失败：" + ex; }
            finally
            {
                _stop.Dispose();
                lock (owner._cyclicGate)
                {
                    foreach (var row in rows) row.Batches.Remove(this);
                    _finished.TrySetResult();
                }
            }
        }
    }

    internal sealed class CanCyclicConfiguration
    {
        private readonly CanConnectionResource _owner;
        internal CyclicSlot Slot;
        internal readonly string TaskName, Kind;
        internal readonly long Generation;
        internal readonly int Row;
        internal readonly CanCyclicDefinition Definition;
        internal CanPayloadProcessor Processor;
        internal CanCyclicControl Control;
        internal long TickDue;
        internal bool Superseded, Stopped, AttemptedThisTick;
        internal string? Error;
        private long _lastSample, _submitted, _lastSubmission;
        private DateTimeOffset? _lastSubmissionUtc;
        private Task _sample = Task.CompletedTask;
        private Task<CanMessage>? _build;
        private long _controlRevision, _buildRevision;
        private Task? _retirement;
        private Task? _drain;
        internal bool HasUnfinishedWork => _native != 0 || !_sample.IsCompleted || _build is { IsCompleted: false }
            || _actual.Any(t => !t.IsCompleted) || Batches.Count != 0 || _drain is { IsCompleted: false } || _retirement is { IsCompleted: false };
        internal readonly HashSet<CanCyclicBatch> Batches = [];
        internal void CancelBatches() { foreach (var batch in Batches) batch.Cancel(); }
        internal bool Retired => _retirement?.IsCompleted == true;
        internal void Retire()
        {
            CancelBatches();
            _retirement ??= Task.Run(DrainAsync);
            _ = _retirement.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
        private readonly HashSet<Task> _actual = [];
        private int _native;
        internal bool NativePending => _native != 0;
        private TaskCompletionSource _nativeExited = CompletedSource();
        internal CanCyclicConfiguration(CanConnectionResource owner, CyclicSlot slot, string name, string kind, long generation,
            int row, CanCyclicDefinition definition, CanPayloadProcessor processor)
        { _owner = owner; Slot = slot; TaskName = name; Kind = kind; Generation = generation; Row = row; Definition = definition; Processor = processor; Control = definition.Control; }
        internal bool IsUsable => !Stopped && !Superseded && Error == null && ReferenceEquals(Slot.Current, this);
        internal bool IsDue { get { lock (_owner._cyclicGate) return IsUsable && Control.Enabled && Stopwatch.GetTimestamp() >= Slot.NextDue; } }
        internal void Check()
        {
            Definition.CheckLifetime();
            lock (_owner._cyclicGate) if (!IsUsable) throw new CanCyclicChangedException();
        }
        internal void StartControlSample(CancellationToken token)
        {
            lock (_owner._cyclicGate)
            {
                if (Definition.RefreshMs <= 0 || !IsUsable || !_sample.IsCompleted || Stopwatch.GetElapsedTime(_lastSample).TotalMilliseconds < Definition.RefreshMs) return;
                _lastSample = Stopwatch.GetTimestamp();
                _sample = Task.Run(async () =>
                {
                    try
                    {
                        var control = await TrackAsync(() => Definition.Sample(token, Check), token).ConfigureAwait(false);
                        CanDynamicCyclicLoop.Validate(control);
                        lock (_owner._cyclicGate)
                        {
                            if (!IsUsable) return;
                            if (Control != control)
                            { _controlRevision++; CancelBatches(); Slot.NextDue = AddCyclicPeriod(Stopwatch.GetTimestamp(), control.PeriodMs); }
                            Control = control;
                        }
                    }
                    catch (CanCyclicChangedException) { }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                    catch (Exception ex) { Fail(ex); }
                }, CancellationToken.None);
            }
        }
        internal bool BuildReady { get { lock (_owner._cyclicGate) return _build?.IsCompleted == true; } }
        internal void StartBuild(CancellationToken token)
        {
            lock (_owner._cyclicGate)
            {
                if (!IsUsable || _build != null) return;
                TickDue = Slot.NextDue; _buildRevision = _controlRevision;
                long revision = _buildRevision;
                void CheckBuild()
                { Check(); lock (_owner._cyclicGate) if (_controlRevision != revision || !Control.Enabled) throw new CanCyclicChangedException(); }
                _build = Task.Run(() => TrackAsync(() => Definition.Build(token, CheckBuild), token), CancellationToken.None);
                _ = _build.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
        internal Task<CanMessage> TakeBuiltAsync()
        {
            lock (_owner._cyclicGate)
            {
                var result = _build ?? throw new InvalidOperationException("发送池报文尚未准备"); _build = null;
                if (_buildRevision != _controlRevision) throw new CanCyclicChangedException();
                return result;
            }
        }
        private async Task<T> TrackAsync<T>(Func<Task<T>> work, CancellationToken token)
        {
            Task<T> actual;
            lock (_owner._cyclicGate) { Check(); actual = Task.Run(work, CancellationToken.None); _actual.Add(actual); }
            _ = actual.ContinueWith(t => { _ = t.Exception; lock (_owner._cyclicGate) _actual.Remove(t); }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            T result;
            try { result = await actual.WaitAsync(TimeSpan.FromMilliseconds(Definition.TimeoutMs), token).ConfigureAwait(false); }
            catch (TimeoutException ex) { throw new TimeoutException($"周期任务 {TaskName} 第 {Row} 行求值超过 {Definition.TimeoutMs} ms，真实求值仍被跟踪", ex); }
            Check(); return result;
        }
        internal bool MoveTo(CanMessage frame)
        {
            lock (_owner._cyclicGate)
            {
                if (!IsUsable) return false;
                var key = (frame.FrameType, frame.Id);
                if (_owner._cyclicSlots.TryGetValue(key, out var next) && ReferenceEquals(next, Slot)) return true;
                if (next != null && (next.LatestGeneration > Generation || next.LatestGeneration == Generation && next.Current != null))
                { Superseded = true; Retire(); return false; }
                var processor = next?.Processor?.Reconfigure(Definition.Protection) ?? new CanPayloadProcessor(Definition.Protection);
                if (next == null)
                {
                    if (_owner._cyclicSlots.Count >= 4096) throw new InvalidOperationException("动态 CAN ID 槽位超过 4096 个");
                    _owner._cyclicSlots.Add(key, next = new() { NextDue = AddCyclicPeriod(Stopwatch.GetTimestamp(), Control.PeriodMs) });
                }
                if (next.Current is { } old) { old.Superseded = true; old.Retire(); }
                Slot.Current = null; Slot = next; next.Current = this; next.LatestGeneration = Generation;
                Processor = processor; next.Processor = processor;
                TickDue = next.NextDue;
                return Stopwatch.GetTimestamp() >= next.NextDue;
            }
        }
        internal IDisposable EnterNative(CanPayloadProcessor processor)
        {
            lock (_owner._cyclicGate)
            {
                Check();
                if (!Control.Enabled || _buildRevision != _controlRevision) throw new CanCyclicChangedException();
                processor.CommitAttempt(); AttemptedThisTick = true;
                if (_native++ == 0) _nativeExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
                return new NativeLease(this);
            }
        }
        internal void Submitted()
        { lock (_owner._cyclicGate) { _submitted++; _lastSubmission = Stopwatch.GetTimestamp(); _lastSubmissionUtc = DateTimeOffset.UtcNow; } }
        internal void Fail(Exception error)
        { lock (_owner._cyclicGate) if (IsUsable) { Error = error.ToString(); CancelBatches(); } }
        internal Task DrainAsync()
        { lock (_owner._cyclicGate) return _drain ??= Task.Run(DrainCoreAsync); }

        private async Task DrainCoreAsync()
        {
            // A queued sampler may register its actual task after the first snapshot. Keep draining until none remain.
            while (true)
            {
                Task[] pending;
                lock (_owner._cyclicGate) pending = _actual.Append(_sample).Append(_build ?? Task.CompletedTask).Append(_nativeExited.Task)
                    .Concat(Batches.Select(b => b.Completion)).Where(t => !t.IsCompleted).ToArray();
                if (pending.Length == 0) break;
                await Task.WhenAll(pending.Select(t => t.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default))).ConfigureAwait(false);
            }
            await Definition.Drain().ConfigureAwait(false);
        }
        internal object Snapshot() => new
        {
            Row, Generation, Kind, Id = _owner._cyclicSlots.FirstOrDefault(p => ReferenceEquals(p.Value, Slot)).Key.Item2,
            FrameType = Definition.FrameType.ToString(), IsFd = Definition.Fd,
            State = Superseded ? "Superseded" : Stopped ? HasUnfinishedWork ? "Stopping" : "Stopped" : Error != null ? "Faulted" : Control.Enabled ? "Sending" : "Paused",
            Owned = IsUsable, Enabled = Control.Enabled, CycleTimeMs = Control.PeriodMs, Submitted = _submitted,
            Active = IsUsable && Control.Enabled,
            LastSubmissionMonotonicTicks = _lastSubmission, LastSubmissionUtc = _lastSubmissionUtc,
            NativePending = _native,
            ExitPending = HasUnfinishedWork,
            PendingApply = IsUsable && _owner._cyclicConfigurations.Any(c => ReferenceEquals(c.Slot, Slot) && !ReferenceEquals(c, this) && c._native != 0),
            Error
        };
        private static TaskCompletionSource CompletedSource() { var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); t.SetResult(); return t; }
        private sealed class NativeLease(CanCyclicConfiguration owner) : IDisposable
        {
            public void Dispose() { lock (owner._owner._cyclicGate) if (--owner._native == 0) owner._nativeExited.TrySetResult(); }
        }
    }
}
