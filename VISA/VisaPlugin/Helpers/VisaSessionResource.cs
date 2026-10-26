using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Ivi.Visa;
using PluginCommon;

namespace VISA.Helpers;

/// <summary>拥有真实 VISA 调用、会话句柄和关闭结果；调用者取消等待不等于原生调用退出。</summary>
public sealed class VisaSessionResource : IDisposable
{
    private readonly PendingIoGate _io = new();
    private readonly object _state = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Action<Exception>? _report;
    private IDisposable? _nativeSession;
    private IMessageBasedSession? _session;
    private SubmissionBudget? _activeBudget;
    private int _closeRequested;
    private sealed record SubmissionBudget(long StartedAt, int TimeoutMs, string Description, CancellationToken Caller);
    public int IoTimeoutMs { get; }
    public string Terminator { get; }
    public string? PhysicalResourceKey { get; }
    public bool HasPendingOperation => _io.HasPendingOperation;
    public bool IsClosed => _io.IsClosed;
    public string? IsolatedReason => _io.IsolatedReason;
    public Exception? LastCloseError { get; private set; }
    public Exception? LastCancellationError { get; private set; }

    internal void EnsureAvailableForPublication()
    {
        lock (_state)
            if (_session == null || Volatile.Read(ref _closeRequested) != 0 || _io.IsClosed || _io.IsolatedReason != null)
                throw new InvalidOperationException("VISA 会话已关闭、正在收尾或被隔离，拒绝发布迟到结果");
    }

    public VisaSessionResource(int ioTimeoutMs, string terminator, Action<Exception>? report = null, string? resourceString = null)
    {
        IoTimeoutMs = ioTimeoutMs;
        Terminator = VisaHelper.NormalizeTerminator(terminator);
        _report = report;
        PhysicalResourceKey = resourceString == null ? null : VisaHelper.GetPhysicalResourceKey(resourceString);
    }

    public VisaSessionResource(IMessageBasedSession session, int ioTimeoutMs, string terminator, string? resourceString = null)
        : this(ioTimeoutMs, terminator, resourceString: resourceString) { _nativeSession = session; _session = session; }

    public Task InitializeAsync(Func<IMessageBasedSession> open, int timeoutMs, CancellationToken ct) =>
        InitializeAsync(() => (IDisposable)open(), _ => { }, timeoutMs, ct);

    public async Task InitializeAsync(Func<IDisposable> openRaw, Action<IMessageBasedSession> configure, int timeoutMs, CancellationToken ct)
    {
        await RunOwnedAsync(async token =>
        {
            if (PhysicalResourceKey != null)
                await RetiringResources.WaitForPreviousAsync(PhysicalResourceKey, RemainingSubmissionBudget(), token).ConfigureAwait(false);
            ThrowIfSubmissionStopped();
            if (_nativeSession != null) throw new InvalidOperationException("VISA owner 已持有原生会话，不能重复打开");
            // 原始句柄返回后立刻归 owner；类型检查/配置失败也不能失去重试关闭入口。
            _nativeSession = openRaw() ?? throw new InvalidOperationException("VISA 驱动未返回原生会话");
            ThrowIfSubmissionStopped();
            var session = _nativeSession as IMessageBasedSession
                ?? throw new InvalidOperationException("资源不是消息型 VISA 设备，无法进行 SCPI 通信；原生句柄仍由 owner 持有");
            configure(session);
            ThrowIfSubmissionStopped();
            _session = session;
            return true;
        }, timeoutMs, "VISA 打开及配置", ct).ConfigureAwait(false);
        if (Volatile.Read(ref _closeRequested) != 0)
            throw new InvalidOperationException("VISA 建立期间已收到关闭请求，迟到的会话由原所有者收尾，不能报告连接可用");
    }

    private int RemainingSubmissionBudget()
    {
        ThrowIfSubmissionStopped();
        lock (_state)
        {
            var budget = _activeBudget!;
            if (budget.TimeoutMs == Timeout.Infinite)
                return IoTimeoutMs > 0 ? IoTimeoutMs : throw new InvalidOperationException("等待旧 VISA 收尾需要有限的 Open 或 I/O 期限，不能两者均为无限等待");
            var remaining = budget.TimeoutMs - Stopwatch.GetElapsedTime(budget.StartedAt).TotalMilliseconds;
            if (remaining <= 0) throw new TimeoutException("VISA 建立总期限已到，不能继续等待旧资源收尾");
            return (int)Math.Ceiling(remaining);
        }
    }

    public Task<T> RunBlockingAsync<T>(Func<IMessageBasedSession, T> operation, int timeoutMs, string description, CancellationToken ct) =>
        RunAsync((session, _) => Task.FromResult(operation(session)), timeoutMs, description, ct);

    public Task RunBlockingAsync(Action<IMessageBasedSession> operation, int timeoutMs, string description, CancellationToken ct) =>
        RunBlockingAsync(session => { operation(session); return true; }, timeoutMs, description, ct);

    public Task<T> RunAsync<T>(Func<IMessageBasedSession, Task<T>> operation, int timeoutMs, string description, CancellationToken ct) =>
        RunAsync((session, _) => operation(session), timeoutMs, description, ct);

    public Task<T> RunAsync<T>(Func<IMessageBasedSession, CancellationToken, Task<T>> operation, int timeoutMs, string description, CancellationToken ct) =>
        RunOwnedAsync(token => operation(GetSession(), token), timeoutMs, description, ct);

    private Task<T> RunOwnedAsync<T>(Func<CancellationToken, Task<T>> operation, int timeoutMs, string description, CancellationToken ct)
    {
        var budget = new SubmissionBudget(Stopwatch.GetTimestamp(), timeoutMs, description, ct);
        return _io.RunAsync(async () =>
        {
            lock (_state) _activeBudget = budget;
            var cancellation = new VisaIoCancellation(ct, _stop.Token, timeoutMs == Timeout.Infinite ? Timeout.Infinite
                : Math.Max(1, (int)Math.Ceiling(timeoutMs - Stopwatch.GetElapsedTime(budget.StartedAt).TotalMilliseconds)));
            T result = default!;
            Exception? failure = null;
            try
            {
                ThrowIfSubmissionStopped();
                try { result = await operation(cancellation.Token).ConfigureAwait(false); }
                catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && !_stop.IsCancellationRequested && cancellation.IsCancellationRequested)
                { throw new TimeoutException($"{description}：复合事务总期限已到，原始调用已退出", ex); }
                ThrowIfSubmissionStopped();
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try { await cancellation.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup)
                {
                    failure = failure == null ? cleanup : new AggregateException("VISA 原始调用与取消回调均失败", failure, cleanup);
                    LastCancellationError = failure;
                    QueueCancellationReport(failure);
                }
                finally { lock (_state) _activeBudget = null; }
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }, timeoutMs, description, ct);
    }

    private void QueueCancellationReport(Exception failure)
    {
        // 真实原生调用/取消回调已经退出。日志故障不能继续占用 I/O 门或延迟句柄释放。
        Task Schedule() => Task.Run(() =>
        {
            try
            {
                Trace.TraceError($"VISA 取消相关事务未正常结束：{failure}");
                _report?.Invoke(failure);
            }
            catch (Exception reportError)
            {
                try { Trace.TraceError($"VISA 取消回调诊断失败：{reportError}"); }
                catch (Exception) { /* 最终诊断接收器也失败；原始聚合异常仍保存在 owner。 */ }
            }
        }, CancellationToken.None);
        Task reporting;
        if (ExecutionContext.IsFlowSuppressed()) reporting = Schedule();
        else { using (ExecutionContext.SuppressFlow()) reporting = Schedule(); }
        _ = reporting.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>每次后续原生提交前检查；不依赖 CancelAfter 回调是否已被线程池调度。</summary>
    public void ThrowIfSubmissionStopped()
    {
        lock (_state)
        {
            var budget = _activeBudget ?? throw new InvalidOperationException("VISA 原生提交必须属于 owner 的活动事务");
            budget.Caller.ThrowIfCancellationRequested();
            if (Volatile.Read(ref _closeRequested) != 0) throw new InvalidOperationException("VISA 已请求关闭，禁止继续提交后续命令");
            if (budget.TimeoutMs != Timeout.Infinite && Stopwatch.GetElapsedTime(budget.StartedAt).TotalMilliseconds >= budget.TimeoutMs)
                throw new TimeoutException($"{budget.Description}：单调总期限已到，禁止继续提交后续命令");
        }
    }

    public async Task CloseAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        Interlocked.Exchange(ref _closeRequested, 1);
        _io.RequestStop();
        TrackRetirement();
        var closing = _io.CloseAsync(Release, timeoutMs, ct);
        ObserveRelease();
        _stop.Cancel();
        try { await closing.ConfigureAwait(false); LastCloseError = null; }
        catch (Exception ex) { LastCloseError = ex; throw; }
    }

    private IMessageBasedSession GetSession() => _session
        ?? throw new InvalidOperationException("VISA 会话尚未完成打开，不能提交 I/O");

    private void Release()
    {
        // Only PendingIoGate's confirmed close owns this callback. Never clear the handle on failure.
        _nativeSession?.Dispose();
        _nativeSession = null;
        _session = null;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _closeRequested, 1);
        _io.RequestStop();
        TrackRetirement();
        _io.RequestClose(Release, ex =>
        {
            LastCloseError = ex;
            Trace.TraceError($"VISA 会话收尾未确认，原始调用/句柄仍由资源持有：{ex}");
            _report?.Invoke(ex);
        });
        ObserveRelease();
        _stop.Cancel();
    }
    private void TrackRetirement()
    {
        if (PhysicalResourceKey != null && !_io.IsClosed)
            RetiringResources.Track(PhysicalResourceKey, this, CloseAsync, () => _io.IsClosed, () => _io.IsolatedReason ?? "VISA 正在关闭");
    }
    private void ObserveRelease()
    {
        if (PhysicalResourceKey != null && _io.CloseCompletion is { } actual)
            _ = actual.ContinueWith(_ => RetiringResources.ForgetReleased(PhysicalResourceKey, this), CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
