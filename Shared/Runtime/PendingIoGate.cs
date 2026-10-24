using System.Diagnostics;

namespace PluginCommon;

/// <summary>
/// Serializes actual I/O lifetimes, not just callers waiting for them. A cancelled or
/// timed-out submitted transaction stays isolated until this owner is closed.
/// Compiled privately into each plugin; does not own SDK-specific abort semantics.
/// </summary>
internal sealed class PendingIoGate
{
    private readonly object _state = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Task? _pending;
    private Task? _closeTask;
    private bool _closing;
    private bool _closed;
    private string? _isolatedReason;

    public bool HasPendingOperation { get { lock (_state) return _pending is { IsCompleted: false }; } }
    public bool IsClosed { get { lock (_state) return _closed; } }
    // The real worker, not a bounded wait wrapper. Owners may observe successful late
    // cleanup to remove their retirement record without inventing a new timeout loop.
    public Task? CloseCompletion { get { lock (_state) return _closeTask; } }
    public string? IsolatedReason { get { lock (_state) return _isolatedReason; } }

    public async Task<T> RunAsync<T>(Func<Task<T>> operation, int timeoutMs, string description, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (timeoutMs == 0 || timeoutMs < Timeout.Infinite)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "通信期限必须大于 0，或使用 -1 表示无限等待");
        ct.ThrowIfCancellationRequested();
        CheckAvailable();
        var clock = Stopwatch.StartNew();
        if (!await _gate.WaitAsync(timeoutMs, ct).ConfigureAwait(false))
            throw new TimeoutException($"{description}：等待连接事务超过 {timeoutMs} ms，未提交新的 I/O");

        Task<T>? actual = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            CheckAvailable();
            int remaining = timeoutMs == Timeout.Infinite ? Timeout.Infinite
                : timeoutMs - (int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds);
            if (timeoutMs != Timeout.Infinite && remaining <= 0)
                throw new TimeoutException($"{description}：事务期限已到，未提交新的 I/O");

            // The factory is invoked only after cancellation/ownership checks and off the UI thread.
            // No caller token is used to cancel Task.Run itself: this task must represent real exit.
            actual = Task.Run(async () =>
            {
                // Task.Run may not enter until its caller has already timed out or begun
                // closing. Revalidate at the submission boundary, not just before scheduling.
                lock (_state)
                {
                    ct.ThrowIfCancellationRequested();
                    CheckAvailable();
                    if (timeoutMs != Timeout.Infinite && clock.ElapsedMilliseconds >= timeoutMs)
                        throw new TimeoutException($"{description}：提交前总期限已到，未执行新的 I/O");
                }
                return await operation().ConfigureAwait(false);
            }, CancellationToken.None);
            lock (_state) _pending = actual;

            try
            {
                var wait = timeoutMs == Timeout.Infinite ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(remaining);
                var result = await actual.WaitAsync(wait, ct).ConfigureAwait(false);
                // Another cancellation callback or a delayed continuation may run before
                // WaitAsync observes cancellation/deadline. Timer notification is not a result fence.
                ct.ThrowIfCancellationRequested();
                if (timeoutMs != Timeout.Infinite && clock.ElapsedMilliseconds >= timeoutMs)
                    throw new TimeoutException($"{description}：结果返回时已超过总期限 {timeoutMs} ms");
                CheckAvailable();
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Isolate($"{description}：调用方停止等待，原始 I/O 已退出={actual.IsCompleted}，必须关闭后重新打开");
                throw;
            }
            catch (TimeoutException ex)
            {
                string limit = timeoutMs == Timeout.Infinite ? "底层驱动报告超时（步骤配置无限等待）" : $"超过 {timeoutMs} ms";
                Isolate($"{description}：{limit}，原始 I/O 已退出={actual.IsCompleted}，必须关闭后重新打开");
                throw new TimeoutException(IsolatedReason, ex);
            }
            catch (Exception ex)
            {
                Isolate($"{description}：I/O 失败（{ex.GetType().Name}），必须关闭后重新打开");
                throw;
            }
        }
        finally
        {
            if (actual == null) _gate.Release();
            else if (actual.IsCompleted) FinishOperation(actual);
            else
            {
                _ = actual.ContinueWith(FinishOperation, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
    }

    public Task RunAsync(Func<Task> operation, int timeoutMs, string description, CancellationToken ct) =>
        RunAsync(async () => { await operation().ConfigureAwait(false); return true; }, timeoutMs, description, ct);

    public Task<T> RunBlockingAsync<T>(Func<T> operation, int timeoutMs, string description, CancellationToken ct) =>
        RunAsync(() => Task.FromResult(operation()), timeoutMs, description, ct);

    public Task RunBlockingAsync(Action operation, int timeoutMs, string description, CancellationToken ct) =>
        RunBlockingAsync(() => { operation(); return true; }, timeoutMs, description, ct);

    /// <summary>
    /// Freezes new operations, waits for real I/O exit, then releases the SDK object.
    /// A timeout only stops the caller's wait; the same close task remains owned and retryable.
    /// A completed failed SDK release may be retried, never duplicated while still running.
    /// </summary>
    public async Task CloseAsync(Action release, int timeoutMs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        ct.ThrowIfCancellationRequested();
        Task actualClose;
        lock (_state)
        {
            if (_closed) return;
            _closing = true;
            if (_closeTask == null || _closeTask.IsFaulted || _closeTask.IsCanceled)
            {
                _closeTask = Task.Run(() => CloseCoreAsync(release), CancellationToken.None);
                _ = _closeTask.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            actualClose = _closeTask;
        }
        try { await actualClose.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
        catch (TimeoutException ex) when (!actualClose.IsCompleted)
        {
            throw new TimeoutException($"关闭资源超过 {timeoutMs} ms；真实 I/O 或驱动释放尚未退出，保留引用与隔离，不能报告已关闭", ex);
        }
    }

    public void RequestClose(Action release, Action<Exception>? report = null)
    {
        // IDisposable cannot await. Keep the actual close task alive, do not dispose a handle
        // from underneath native code, and make failure observable to the owning plugin.
        _ = ObserveCloseAsync(release, report);
    }

    private async Task ObserveCloseAsync(Action release, Action<Exception>? report)
    {
        try { await CloseAsync(release, 2000, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex)
        {
            Trace.TraceError($"资源异步收尾未确认：{ex}");
            if (report != null)
            {
                try { report(ex); }
                catch (Exception callbackError) { Trace.TraceError($"资源收尾诊断回调失败：{callbackError}"); }
            }
        }
    }

    private async Task CloseCoreAsync(Action release)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            release();
            lock (_state) { _closed = true; _isolatedReason = null; }
        }
        catch (Exception ex)
        {
            Isolate($"驱动释放失败（{ex.GetType().Name}），资源仍隔离，可重试关闭");
            throw;
        }
        finally { _gate.Release(); }
    }

    private void CheckAvailable()
    {
        lock (_state)
        {
            if (_closed) throw new InvalidOperationException("连接资源已关闭，请重新打开");
            if (_closing) throw new InvalidOperationException("连接资源正在收尾，不能提交新的 I/O");
            if (_isolatedReason != null) throw new InvalidOperationException($"连接资源已隔离：{_isolatedReason}");
        }
    }

    private void Isolate(string reason) { lock (_state) _isolatedReason ??= reason; }

    private void FinishOperation(Task actual)
    {
        if (actual.IsFaulted) _ = actual.Exception;
        lock (_state) { if (ReferenceEquals(_pending, actual)) _pending = null; }
        _gate.Release();
    }
}
