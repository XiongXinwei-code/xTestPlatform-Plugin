using System.Runtime.CompilerServices;
using LIN.Adapters;
using LIN.Models;
using PluginCommon;

namespace LIN.Runtime;

/// <summary>拥有 LIN I/O 与周期任务。取消等待不释放仍在原生调用中的驱动。</summary>
public sealed class LinConnectionResource : ILinAdapter
{
    private static readonly ConditionalWeakTable<ILinAdapter, LinConnectionResource> Wrappers = new();
    private readonly ILinAdapter _raw;
    private readonly PendingIoGate _io = new();
    private readonly OwnerStopSignal _foregroundWait = new();
    private readonly SemaphoreSlim _transactionGate = new(1, 1);
    private readonly AsyncLocal<bool> _insideTransaction = new();
    private readonly AsyncLocal<bool> _insideWorker = new();
    private readonly object _state = new();
    private readonly List<LinCyclicJob> _jobs = [];
    private readonly CancellationTokenSource _lifetimeStop = new();
    private Task _cancelCallbacks = Task.CompletedTask;
    private string? _retirementKey;
    private bool _closing;
    private Task? _close;
    private Exception? _nativeCancellationError;
    public Exception? BackgroundError { get; private set; }
    public bool IsClosed => _io.IsClosed;
    internal bool IsClosing { get { lock (_state) return _closing; } }
    public LinCyclicJob[] Jobs { get { lock (_state) return _jobs.ToArray(); } }
    public bool IsConnected { get { lock (_state) return !_closing && _raw.IsConnected; } }
    public LinConnectionResource(ILinAdapter raw, string? retirementKey = null)
    {
        _raw = raw; _retirementKey = retirementKey;
    }
    public static LinConnectionResource For(ILinAdapter adapter) => adapter as LinConnectionResource ?? Wrappers.GetValue(adapter, a => new(a));
    internal static string PhysicalKey(LinAdapterType type, string channel) => $"LIN:{type}:{channel.Trim().ToUpperInvariant()}";
    public void Open(LinAdapterConfig config) => OpenAsync(config, 5000, default).GetAwaiter().GetResult();
    public Task OpenAsync(LinAdapterConfig config, int timeoutMs, CancellationToken ct) => OpenAsync(config, timeoutMs, ct, null);
    internal async Task OpenAsync(LinAdapterConfig config, int timeoutMs, CancellationToken ct, Action? checkExecution)
    {
        lock (_state) { Check(); _retirementKey ??= PhysicalKey(LinAdapterType.NI, config.Channel); }
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        void BeforeSubmit()
        {
            checkExecution?.Invoke();
            ct.ThrowIfCancellationRequested(); _lifetimeStop.Token.ThrowIfCancellationRequested(); Check();
            if (System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException("LIN 打开在后续原生提交前已超过总期限");
        }
        await _io.RunBlockingAsync(() =>
        {
            BeforeSubmit();
            if (_raw is ILinOperationBoundary boundary) boundary.Open(config, BeforeSubmit); else _raw.Open(config);
        }, timeoutMs, "LIN 打开", ct).ConfigureAwait(false);
        Check();
    }
    private void Check() { lock (_state) if (_closing) throw new InvalidOperationException("LIN 连接正在关闭，不能提交新操作"); }
    public void Write(LinFrame frame) => Write(frame, default, cancelWait: !_insideWorker.Value);
    internal void Write(LinFrame frame, CancellationToken ct, int timeoutMs = 3000, bool cancelWait = false, Action? checkExecution = null)
    {
        checkExecution?.Invoke();
        using var transaction = EnterOperation(timeoutMs, ct);
        lock (_state) { ct.ThrowIfCancellationRequested(); Check(); }
        // 取消只能阻止尚未开始的写；已进入原生写必须由 gate 追踪到真实退出。
        // A cyclic worker must remain joined until its submitted write exits (or fails its
        // own deadline); cancelling only its waiter would falsely report the job stopped.
        _io.RunBlockingAsync(() => { checkExecution?.Invoke(); ct.ThrowIfCancellationRequested(); _lifetimeStop.Token.ThrowIfCancellationRequested(); _raw.Write(frame); },
            transaction.Remaining, "LIN 发送", cancelWait ? ct : default, retireWaitOnClose: cancelWait).GetAwaiter().GetResult();
    }
    public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => ReadCore(null, timeoutMs, ct);
    public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => ReadCore(id, timeoutMs, ct);
    internal LinFrame? ReadCore(byte? id, int timeoutMs, CancellationToken ct, Action? checkExecution = null)
    {
        checkExecution?.Invoke();
        using var transaction = EnterOperation(timeoutMs, ct);
        Check();
        int remaining = transaction.Remaining;
        int wait = remaining < 0 ? Timeout.Infinite : (int)Math.Min(int.MaxValue, (long)remaining + 100);
        return _io.RunBlockingAsync(() =>
        {
            // Keep native cancellation links inside the tracked real worker. Disposing
            // a link on the foreground thread can join a blocked driver callback.
            var cancellation = new LinIoCancellation(ct, _lifetimeStop.Token, Timeout.Infinite);
            LinFrame? result = null; Exception? failure = null;
            try
            {
                checkExecution?.Invoke(); ct.ThrowIfCancellationRequested(); cancellation.Token.ThrowIfCancellationRequested();
                result = id.HasValue ? _raw.Read(id.Value, remaining, cancellation.Token) : _raw.Read(remaining, cancellation.Token);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try { cancellation.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception cleanup)
                {
                    failure = failure == null ? cleanup : new AggregateException("LIN 原始接收与取消回调均失败", failure, cleanup);
                    Interlocked.CompareExchange(ref _nativeCancellationError, failure, null);
                }
            }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }, wait, "LIN 接收", ct).GetAwaiter().GetResult();
    }

    internal Task<LinFrame?> WriteReadAsync(LinFrame frame, int sendTimeoutMs, int responseTimeoutMs, Action sent, CancellationToken ct, Action? checkExecution = null)
        => Task.Run(() =>
        {
            using var transaction = EnterOperation(sendTimeoutMs, ct);
            _insideTransaction.Value = true;
            try
            {
                Write(frame, ct, transaction.Remaining, cancelWait: true, checkExecution);
                sent();
                ct.ThrowIfCancellationRequested(); Check();
                return ReadCore(frame.FrameId, responseTimeoutMs, ct, checkExecution);
            }
            finally { _insideTransaction.Value = false; }
        }, CancellationToken.None);

    private OperationLease EnterOperation(int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0 && timeoutMs != Timeout.Infinite) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        ct.ThrowIfCancellationRequested(); _foregroundWait.ThrowIfStopped("LIN 事务"); Check();
        if (_insideTransaction.Value) return new(null, timeoutMs, started);
        if (!_foregroundWait.EnterAsync(_transactionGate, timeoutMs, ct, "LIN 事务排队").GetAwaiter().GetResult())
            throw new TimeoutException($"等待 LIN 事务门超过 {timeoutMs} ms，未提交操作");
        try { ct.ThrowIfCancellationRequested(); _foregroundWait.ThrowIfStopped("LIN 事务"); Check(); return new(_transactionGate, timeoutMs, started); }
        catch { _transactionGate.Release(); throw; }
    }

    private sealed class OperationLease(SemaphoreSlim? gate, int timeoutMs, long started) : IDisposable
    {
        public int Remaining
        {
            get
            {
                if (timeoutMs == Timeout.Infinite) return Timeout.Infinite;
                int left = timeoutMs - (int)Math.Min(int.MaxValue, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                return left > 0 ? left : throw new TimeoutException("LIN 操作在实际提交前已超过等待期限");
            }
        }
        public void Dispose() => gate?.Release();
    }
    public void Wakeup(bool remote = true) => Wakeup(remote, 3000, default);
    internal void Wakeup(bool remote, int timeoutMs, CancellationToken ct, Action? checkExecution = null)
    {
        using var transaction = EnterOperation(timeoutMs, ct);
        void BeforeSubmit() { checkExecution?.Invoke(); Check(); ct.ThrowIfCancellationRequested(); _ = transaction.Remaining; }
        BeforeSubmit();
        _io.RunBlockingAsync(() =>
        {
            BeforeSubmit();
            if (_raw is ILinOperationBoundary boundary) boundary.Wakeup(remote, BeforeSubmit); else _raw.Wakeup(remote);
        }, transaction.Remaining, "LIN 唤醒", ct).GetAwaiter().GetResult();
    }
    public void Sleep(bool remote = true) => Sleep(remote, 3000, default);
    internal void Sleep(bool remote, int timeoutMs, CancellationToken ct) => SleepAsync(remote, timeoutMs, ct).GetAwaiter().GetResult();
    internal Task SleepAsync(bool remote, int timeoutMs, CancellationToken ct, Action? checkExecution = null) => Task.Run(async () =>
    {
        using var transaction = EnterOperation(timeoutMs, ct); Check();
        await _io.RunBlockingAsync(() => { checkExecution?.Invoke(); Check(); ct.ThrowIfCancellationRequested(); _raw.Sleep(remote); }, transaction.Remaining, "LIN 睡眠", ct).ConfigureAwait(false);
    }, CancellationToken.None);

    public LinCyclicJob StartJob(Func<CancellationToken, Task>[] workers)
    {
        lock (_state)
        {
            if (_closing || _io.IsolatedReason != null) throw new InvalidOperationException("LIN 连接已关闭或隔离，不能启动周期任务");
            var job = new LinCyclicJob(workers.Select(work => new Func<CancellationToken, Task>(async ct =>
            {
                _insideWorker.Value = true;
                try { await work(ct).ConfigureAwait(false); }
                finally { _insideWorker.Value = false; }
            })).ToArray());
            _jobs.Add(job);
            return job;
        }
    }

    public async Task CloseAsync(int timeoutMs = 3000, CancellationToken ct = default)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        Task actual;
        lock (_state)
        {
            _closing = true;
            // Retire foreground/queued observers before cancellation callbacks or
            // cyclic workers can block cleanup. The gate still owns actual native I/O.
            _foregroundWait.RequestStop();
            _io.RequestStop();
            if (_retirementKey != null)
                RetiringResources.Track(_retirementKey, this, CloseAsync, () => IsClosed,
                    () => $"LIN 正在收尾；未退出I/O={_io.HasPendingOperation}；隔离={_io.IsolatedReason}；错误={BackgroundError?.Message}");
            if (!_lifetimeStop.IsCancellationRequested) _cancelCallbacks = _lifetimeStop.CancelAsync();
            foreach (var job in _jobs) job.Cancel();
            if (_close == null || (!_io.IsClosed && (_close.IsFaulted || _close.IsCanceled)))
            {
                _close = Task.Run(CloseCore);
                _ = _close.ContinueWith(t => { BackgroundError = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }
            actual = _close;
        }
        await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
    }

    private async Task CloseCore()
    {
        LinCyclicJob[] jobs;
        lock (_state) jobs = _jobs.ToArray();
        foreach (var job in jobs) job.Cancel();
        // 不使用步骤取消令牌截断实际收尾；外层只有有界等待。
        Exception? callbackError = null;
        try { await Task.WhenAll(jobs.Select(j => j.Completion).Append(_cancelCallbacks)).ConfigureAwait(false); }
        catch (Exception ex) { callbackError = ex; } // All workers/callbacks have exited; still release the owned driver.
        Exception? releasedWithError = null;
        try { await _io.CloseAsync(() =>
        {
            try { _raw.Close(); }
            catch (Exception ex) when (_raw is ILinCloseState { HandlesReleased: true }) { releasedWithError = ex; }
        }, int.MaxValue, default).ConfigureAwait(false); }
        finally { if (_retirementKey != null) RetiringResources.ForgetReleased(_retirementKey, this); }
        var errors = jobs.Select(j => j.Error).Where(e => e != null).Cast<Exception>().ToArray();
        if (callbackError != null) errors = [.. errors, callbackError];
        if (releasedWithError != null) errors = [.. errors, releasedWithError];
        if (Volatile.Read(ref _nativeCancellationError) is { } cancellationError) errors = [.. errors, cancellationError];
        if (errors.Length != 0) throw new AggregateException("LIN 已释放，但周期任务存在失败", errors);
    }
    public void Close() => CloseAsync().GetAwaiter().GetResult();
    public void Dispose()
    {
        var close = CloseAsync(); // Freeze managed admission before returning to the SDK.
        _ = Task.Run(() => ObserveClose(close), CancellationToken.None);
    }
    private async Task ObserveClose(Task close)
    {
        try { await close.ConfigureAwait(false); }
        catch (Exception ex) { BackgroundError = ex; System.Diagnostics.Trace.TraceError($"LIN 异步收尾未确认：{ex}"); }
    }
}

public sealed class LinCyclicJob : IDisposable
{
    internal string? RegistrationKey { get; set; }
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private Task _callbacks = Task.CompletedTask;
    private readonly Task _work;
    private Exception? _error;
    public Exception? Error => Volatile.Read(ref _error);
    public Task Completion { get { lock (_gate) return Task.WhenAll(_work, _callbacks); } }
    internal LinCyclicJob(Func<CancellationToken, Task>[] workers)
    {
        ExecutionScopedExecutor.Own(Cancel);
        _work = Task.WhenAll(workers.Select(work => Task.Run(async () =>
        {
            try { await work(_stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (Exception ex) { Interlocked.CompareExchange(ref _error, ex, null); Cancel(); }
        })));
    }
    public void Cancel() { lock (_gate) if (!_stop.IsCancellationRequested) _callbacks = _stop.CancelAsync(); }
    public async Task StopAsync(int timeoutMs = 3000, CancellationToken ct = default)
    {
        Cancel();
        await Completion.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
        if (Error != null) throw new InvalidOperationException("LIN 周期任务失败，已停止工作单元；请检查连接隔离及驱动状态", Error);
    }
    public void Dispose() => Cancel();
}

internal static class LinRegistryGuard
{
    private static readonly ConditionalWeakTable<object, SemaphoreSlim> Gates = new();
    public static async Task<IDisposable> Enter(object registry, CancellationToken ct, int timeoutMs = Timeout.Infinite)
    {
        var gate = Gates.GetValue(registry, _ => new(1, 1));
        if (!await gate.WaitAsync(timeoutMs, ct).ConfigureAwait(false)) throw new TimeoutException($"LIN 等待连接登记门超过 {timeoutMs} ms");
        return new Lease(gate);
    }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable { public void Dispose() => gate.Release(); }
}
