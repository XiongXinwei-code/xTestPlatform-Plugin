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
    private readonly OwnerStopSignal _stop = new();
    private readonly Func<Func<Task>, Task> _scheduleClose;
    private Task? _pending;
    private Task? _closeTask;
    private bool _closing;
    private bool _closed;
    private string? _isolatedReason;

    internal PendingIoGate() : this(ScheduleClose) { }
    internal PendingIoGate(Func<Func<Task>, Task> scheduleClose) => _scheduleClose = scheduleClose;

    private static Task ScheduleClose(Func<Task> work)
    {
        // Resource teardown is an independent lifetime. A close scheduled before
        // caller cancellation must not later inherit that retired step's guards.
        if (ExecutionContext.IsFlowSuppressed()) return Task.Run(work, CancellationToken.None);
        using (ExecutionContext.SuppressFlow()) return Task.Run(work, CancellationToken.None);
    }

    public bool HasPendingOperation { get { lock (_state) return _pending is { IsCompleted: false }; } }
    public bool IsClosed { get { lock (_state) return _closed; } }
    // The real worker, not a bounded wait wrapper. Owners may observe successful late
    // cleanup to remove their retirement record without inventing a new timeout loop.
    public Task? CloseCompletion { get { lock (_state) return _closeTask; } }
    public string? IsolatedReason { get { lock (_state) return _isolatedReason; } }

    public async Task<T> RunAsync<T>(Func<Task<T>> operation, int timeoutMs, string description, CancellationToken ct, bool retireWaitOnClose = true)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (timeoutMs == 0 || timeoutMs < Timeout.Infinite)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "通信期限必须大于 0，或使用 -1 表示无限等待");
        ct.ThrowIfCancellationRequested();
        CheckAvailable();
        var clock = Stopwatch.StartNew();
        if (!await _stop.EnterAsync(_gate, timeoutMs, ct, description).ConfigureAwait(false))
            throw new TimeoutException($"{description}：等待连接事务超过 {timeoutMs} ms，未提交新的 I/O");

        Task<T>? actual = null;
        IDisposable? stepRetirement = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            CheckAvailable();
            int remaining = timeoutMs == Timeout.Infinite ? Timeout.Infinite
                : timeoutMs - (int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds);
            if (timeoutMs != Timeout.Infinite && remaining <= 0)
                throw new TimeoutException($"{description}：事务期限已到，未提交新的 I/O");
            stepRetirement = ExecutionScopedExecutor.OwnOperation(() =>
                Isolate($"{description}：步骤已退出，已提交 I/O 的结果尚未消费，保留原事务直到真实退出"));

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
                // A foreground observer may retire. A background owner that reports
                // worker exit must keep awaiting the actual submitted I/O instead.
                var result = retireWaitOnClose
                    ? await _stop.WaitAsync(actual, wait, ct, description).ConfigureAwait(false)
                    : await actual.WaitAsync(wait, ct).ConfigureAwait(false);
                // Another cancellation callback or a delayed continuation may run before
                // WaitAsync observes cancellation/deadline. Timer notification is not a result fence.
                ct.ThrowIfCancellationRequested();
                if (timeoutMs != Timeout.Infinite && clock.ElapsedMilliseconds >= timeoutMs)
                    throw new TimeoutException($"{description}：结果返回时已超过总期限 {timeoutMs} ms");
                CheckAvailable(allowClosing: !retireWaitOnClose);
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
            stepRetirement?.Dispose(); // The result/cancellation fence ran; native ownership remains below.
            if (actual == null) _gate.Release();
            else if (actual.IsCompleted) FinishOperation(actual);
            else
            {
                _ = actual.ContinueWith(FinishOperation, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
    }

    public Task RunAsync(Func<Task> operation, int timeoutMs, string description, CancellationToken ct, bool retireWaitOnClose = true) =>
        RunAsync(async () => { await operation().ConfigureAwait(false); return true; }, timeoutMs, description, ct, retireWaitOnClose);

    public Task<T> RunBlockingAsync<T>(Func<T> operation, int timeoutMs, string description, CancellationToken ct, bool retireWaitOnClose = true) =>
        RunAsync(() => Task.FromResult(operation()), timeoutMs, description, ct, retireWaitOnClose);

    public Task RunBlockingAsync(Action operation, int timeoutMs, string description, CancellationToken ct, bool retireWaitOnClose = true) =>
        RunBlockingAsync(() => { operation(); return true; }, timeoutMs, description, ct, retireWaitOnClose);

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
        var actualClose = BeginClose(release);
        try { await actualClose.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
        catch (TimeoutException ex) when (!actualClose.IsCompleted)
        {
            throw new TimeoutException($"关闭资源超过 {timeoutMs} ms；真实 I/O 或驱动释放尚未退出，保留引用与隔离，不能报告已关闭", ex);
        }
    }

    /// <summary>Freeze admission and retire observers before any driver abort/callback.</summary>
    internal void RequestStop()
    {
        lock (_state) { _closing = true; _stop.RequestStop(); }
    }

    private Task BeginClose(Action release)
    {
        lock (_state)
        {
            if (_closed) return Task.CompletedTask;
            RequestStop();
            if (_closeTask == null || _closeTask.IsFaulted || _closeTask.IsCanceled)
            {
                _closeTask = _scheduleClose(() => CloseCoreAsync(release));
                _ = _closeTask.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            return _closeTask;
        }
    }

    public void RequestClose(Action release, Action<Exception>? report = null)
    {
        // IDisposable cannot await. Keep the actual close task alive, do not dispose a handle
        // from underneath native code, and make failure observable to the owning plugin.
        ArgumentNullException.ThrowIfNull(release);
        var actualClose = BeginClose(release); // Freeze synchronously, observe off the SDK cleanup thread.
        _ = Task.Run(() => ObserveCloseAsync(actualClose, report), CancellationToken.None);
    }

    private static async Task ObserveCloseAsync(Task actualClose, Action<Exception>? report)
    {
        // Automatic cleanup has no arbitrary new deadline. The real close stays
        // tracked; explicit close callers retain their own configured wait budget.
        try { await actualClose.ConfigureAwait(false); }
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

    private void CheckAvailable(bool allowClosing = false)
    {
        lock (_state)
        {
            if (_closed) throw new InvalidOperationException("连接资源已关闭，请重新打开");
            if (_closing && !allowClosing) throw new InvalidOperationException("连接资源正在收尾，不能提交新的 I/O");
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
