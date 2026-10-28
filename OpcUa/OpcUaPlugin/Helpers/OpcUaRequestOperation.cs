using System.Diagnostics;

namespace OpcUa.Helpers;

/// <summary>SDK 取消回调异步运行并归原请求所有；单调预算不依赖回调及时调度。</summary>
internal sealed class OpcUaRequestOperation : IAsyncDisposable
{
    private readonly object _state = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly CancellationToken _caller, _lifetime;
    private readonly CancellationTokenRegistration _callerRegistration, _lifetimeRegistration;
    private readonly Timer _timer;
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private readonly int _timeoutMs;
    private readonly string _description;
    private Task _callbacks = Task.CompletedTask;
    private bool _sealed;

    internal CancellationToken Token { get; }
    internal TimeSpan Remaining => TimeSpan.FromMilliseconds(Math.Max(0,
        _timeoutMs - Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds));
    internal bool IsExpired => Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds >= _timeoutMs;

    internal OpcUaRequestOperation(int timeoutMs, string description, CancellationToken caller, CancellationToken lifetime)
    {
        _timeoutMs = timeoutMs; _description = description; _caller = caller; _lifetime = lifetime; Token = _cancel.Token;
        _callerRegistration = caller.UnsafeRegister(static state => ((OpcUaRequestOperation)state!).RequestCancellation(), this);
        _lifetimeRegistration = lifetime.UnsafeRegister(static state => ((OpcUaRequestOperation)state!).RequestCancellation(), this);
        _timer = new Timer(static state => ((OpcUaRequestOperation)state!).RequestCancellation(), this, timeoutMs, Timeout.Infinite);
    }

    internal void ThrowIfActive()
    {
        _caller.ThrowIfCancellationRequested();
        _lifetime.ThrowIfCancellationRequested();
        if (IsExpired) throw new TimeoutException($"{_description} 的总期限 {_timeoutMs} ms 已到，禁止提交后续请求或发布晚到结果");
        Token.ThrowIfCancellationRequested();
    }

    internal void RequestCancellation()
    {
        lock (_state)
            if (!_sealed && !_cancel.IsCancellationRequested) _callbacks = _cancel.CancelAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // 先冻结所有取消入口，再观察真实回调；不能把 Read 返回等同于取消回调已退出。
        await _timer.DisposeAsync().ConfigureAwait(false);
        await _callerRegistration.DisposeAsync().ConfigureAwait(false);
        await _lifetimeRegistration.DisposeAsync().ConfigureAwait(false);
        Task callbacks;
        lock (_state) { _sealed = true; callbacks = _callbacks; }
        try { await callbacks.ConfigureAwait(false); }
        finally { _cancel.Dispose(); }
    }
}
