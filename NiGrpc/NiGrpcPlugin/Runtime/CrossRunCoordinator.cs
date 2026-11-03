using System.Diagnostics;

namespace NiGrpc.Runtime;

/// <summary>同一插件加载实例内保守协调所有 NI gRPC 执行代次。</summary>
internal sealed class CrossRunCoordinator
{
    internal static CrossRunCoordinator Shared { get; } = new();
    private readonly object _sync = new();
    private Entry? _owner;
    private TaskCompletionSource _changed = NewSignal();
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal async Task<Lease> EnterAsync(object generation, Task stopped, int timeoutMs, CancellationToken ct, bool cleanup = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            Task changed;
            lock (_sync)
            {
                if (stopped.IsCompleted) throw new InvalidOperationException("NI gRPC 执行代次已退出，禁止新的设备操作");
                if (_owner == null)
                {
                    _owner = new Entry(generation, stopped);
                    var observed = _owner;
                    _ = Task.Run(() => ObserveRetirementAsync(observed));
                }
                if (_owner.Unknown != null && !(cleanup && ReferenceEquals(_owner.Generation, generation)))
                    throw new InvalidOperationException($"NI gRPC 跨运行资源已隔离，远端状态未确认：{_owner.Unknown}");
                if (ReferenceEquals(_owner.Generation, generation))
                {
                    if (_owner.Retired && !cleanup) throw new InvalidOperationException("NI gRPC 代次正在退出，禁止新的设备操作");
                    if (cleanup || (_owner.ClosingOperations == 0 && !_owner.Connections.Any(c => c.CloseStarted?.IsCompleted == true) && !_owner.Leases.Any(l => l.BlocksAdmission)))
                    {
                        _owner.Active++;
                        var lease = new Lease(this, _owner, ct);
                        _owner.Leases.Add(lease);
                        return lease;
                    }
                }
                changed = _changed.Task;
            }
            int remaining = clock.ElapsedMilliseconds < timeoutMs ? timeoutMs - (int)clock.ElapsedMilliseconds
                : throw new TimeoutException($"等待前一 NI gRPC 执行代次收尾超过 {timeoutMs} ms，未提交新请求");
            try { await Task.WhenAny(changed, stopped).WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false); }
            catch (TimeoutException ex) { throw new TimeoutException($"等待前一 NI gRPC 执行代次收尾超过 {timeoutMs} ms，未提交新请求", ex); }
        }
    }

    private async Task ObserveRetirementAsync(Entry entry)
    {
        if (await Task.WhenAny(entry.Stopped, entry.Released.Task).ConfigureAwait(false) == entry.Released.Task) return;
        ConnectionEntry[] close;
        lock (_sync)
        {
            if (!ReferenceEquals(_owner, entry)) return;
            entry.Retired = true;
            close = entry.Connections.ToArray();
            entry.Connections.Clear();
            entry.CleanupPending += close.Length;
            PulseLocked();
            TryReleaseLocked(entry);
        }
        foreach (var action in close) _ = Task.Run(() => CloseAsync(entry, action.Close));
    }

    private async Task CloseAsync(Entry entry, Func<Task> close)
    {
        Exception? error = null;
        try { await close().ConfigureAwait(false); }
        catch (Exception ex) { error = ex; }
        lock (_sync)
        {
            if (error != null && !IsConfirmedPending(error))
                entry.Unknown ??= $"旧连接收尾失败：{error.Message}";
            entry.CleanupPending--;
            PulseLocked(); TryReleaseLocked(entry);
        }
    }

    private void PulseLocked() { var old = _changed; _changed = NewSignal(); old.TrySetResult(); }
    private static bool IsConfirmedPending(Exception? error) => error is AggregateException aggregate &&
        aggregate.Flatten().InnerExceptions.Count > 0 && aggregate.Flatten().InnerExceptions.All(e => e is ConfirmedCleanupPendingException);
    private void TryReleaseLocked(Entry entry)
    {
        if (entry.Pending == 0)
        {
            entry.Connections.RemoveAll(c => c.ConfirmedPending);
            entry.ClosingOperations -= entry.DeferredCloseReleases;
            entry.DeferredCloseReleases = 0;
        }
        if (ReferenceEquals(_owner, entry) && entry.Active == 0 && entry.Pending == 0 && entry.CleanupPending == 0 && entry.ClosingOperations == 0 && entry.Connections.Count == 0 && entry.Unknown == null)
        { _owner = null; entry.Released.TrySetResult(); PulseLocked(); }
    }

    internal int ConnectionCount { get { lock (_sync) return _owner?.Connections.Count ?? 0; } }
    internal sealed class ConnectionEntry(Func<Task> close, Task? closeStarted) { internal readonly Func<Task> Close = close; internal readonly Task? CloseStarted = closeStarted; internal bool ConfirmedPending; }

    internal sealed class Entry(object generation, Task stopped)
    {
        internal readonly object Generation = generation;
        internal readonly Task Stopped = stopped;
        internal readonly TaskCompletionSource Released = NewSignal();
        internal readonly List<ConnectionEntry> Connections = [];
        internal readonly HashSet<Lease> Leases = [];
        internal int Active, Pending, CleanupPending, ClosingOperations, DeferredCloseReleases;
        internal bool Retired;
        internal string? Unknown;
    }

    internal sealed class Lease : IDisposable
    {
        private readonly CrossRunCoordinator _owner;
        private readonly Entry _entry;
        private readonly CancellationToken _caller;
        internal bool CallerCancelled => _caller.IsCancellationRequested;
        internal bool BlocksAdmission => CallerCancelled || (_disposed != 0 && _ownPending != 0);
        private int _disposed;
        private int _ownPending;
        internal Lease(CrossRunCoordinator owner, Entry entry, CancellationToken ct)
        {
            _owner = owner; _entry = entry; _caller = ct;
        }
        internal void CheckAdmission(bool cleanup)
        {
            lock (_owner._sync)
            {
                if (_entry.Unknown != null && !cleanup) throw new InvalidOperationException($"NI gRPC 资源已隔离：{_entry.Unknown}");
                if (_entry.Retired || _entry.Stopped.IsCompleted) throw new InvalidOperationException("NI gRPC 代次正在退出，禁止新的设备操作");
                if (!cleanup && (_entry.ClosingOperations != 0 || _entry.Connections.Any(c => c.CloseStarted?.IsCompleted == true)))
                    throw new InvalidOperationException("NI gRPC 连接或会话正在收尾，未提交新请求");
                if (!cleanup && _entry.Leases.Any(l => !ReferenceEquals(l, this) && l.BlocksAdmission))
                    throw new InvalidOperationException("NI gRPC 同代步骤正在取消收尾，未提交新请求");
            }
        }
        internal void RegisterConnection(Func<Task> close, Task? closeCompletion = null, Task? closeStarted = null)
        {
            var registered = new ConnectionEntry(close, closeStarted);
            lock (_owner._sync)
            {
                if (_entry.Retired || _entry.Stopped.IsCompleted || _caller.IsCancellationRequested || _entry.Leases.Any(l => l.BlocksAdmission))
                    throw new InvalidOperationException("NI gRPC 代次已退出，禁止登记新连接");
                _entry.Connections.Add(registered);
            }
            if (closeCompletion != null)
                _ = closeCompletion.ContinueWith(t =>
                {
                    lock (_owner._sync)
                    {
                        if (t.IsCompletedSuccessfully) _entry.Connections.Remove(registered);
                        else if (IsConfirmedPending(t.Exception)) registered.ConfirmedPending = true;
                        else { _ = t.Exception; _entry.Unknown ??= "连接关闭失败，远端状态或本地退出未确认"; }
                        _owner.PulseLocked(); _owner.TryReleaseLocked(_entry);
                    }
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        internal void MarkUnknown(string reason)
        {
            lock (_owner._sync) { _entry.Unknown ??= reason; _owner.PulseLocked(); }
        }
        internal IDisposable BeginSessionClose()
        {
            lock (_owner._sync) { _entry.ClosingOperations++; _owner.PulseLocked(); }
            return new CloseMarker(_owner, _entry);
        }
        internal void Track(Task actual) => TrackCore(actual, false, true);
        internal void TrackCleanup(Task actual) => TrackCore(actual, true, false);
        private void TrackCore(Task actual, bool faultIsUnknown, bool ownStep)
        {
            lock (_owner._sync) { _entry.Pending++; if (ownStep) _ownPending++; }
            _ = actual.ContinueWith(t =>
            {
                lock (_owner._sync)
                {
                    if (t.IsFaulted) { _ = t.Exception; if (faultIsUnknown) _entry.Unknown ??= "本地调用或回调退出未确认"; }
                    _entry.Pending--;
                    if (ownStep && --_ownPending == 0 && _disposed != 0) _entry.Leases.Remove(this);
                    _owner.PulseLocked(); _owner.TryReleaseLocked(_entry);
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (_owner._sync) { if (_ownPending == 0) _entry.Leases.Remove(this); _entry.Active--; _owner.PulseLocked(); _owner.TryReleaseLocked(_entry); }
        }
    }

    private sealed class CloseMarker(CrossRunCoordinator owner, Entry entry) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (owner._sync)
            {
                if (entry.Pending == 0) entry.ClosingOperations--;
                else entry.DeferredCloseReleases++;
                owner.PulseLocked(); owner.TryReleaseLocked(entry);
            }
        }
    }
}
