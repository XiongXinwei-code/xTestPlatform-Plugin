namespace VISA.Helpers;

/// <summary>外部停止只请求异步取消；原事务同时拥有原生调用与其实际取消回调。</summary>
internal sealed class VisaIoCancellation : IAsyncDisposable
{
    private readonly object _state = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly CancellationTokenRegistration _callerRegistration, _ownerRegistration;
    private readonly Timer _timer;
    private Task _callbacks = Task.CompletedTask;
    private bool _sealed;

    internal CancellationToken Token { get; }
    internal bool IsCancellationRequested => Token.IsCancellationRequested;

    internal VisaIoCancellation(CancellationToken caller, CancellationToken owner, int remainingMs)
    {
        Token = _cancel.Token;
        _callerRegistration = caller.UnsafeRegister(static state => ((VisaIoCancellation)state!).RequestCancellation(), this);
        _ownerRegistration = owner.UnsafeRegister(static state => ((VisaIoCancellation)state!).RequestCancellation(), this);
        _timer = new Timer(static state => ((VisaIoCancellation)state!).RequestCancellation(), this, remainingMs, Timeout.Infinite);
    }

    private void RequestCancellation()
    {
        lock (_state)
            if (!_sealed && !_cancel.IsCancellationRequested) _callbacks = _cancel.CancelAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // 先阻止新的取消桥进入，再等待回调；action 返回不代表取消回调已退出。
        await _timer.DisposeAsync().ConfigureAwait(false);
        await _callerRegistration.DisposeAsync().ConfigureAwait(false);
        await _ownerRegistration.DisposeAsync().ConfigureAwait(false);
        Task callbacks;
        lock (_state) { _sealed = true; callbacks = _callbacks; }
        try { await callbacks.ConfigureAwait(false); }
        finally { _cancel.Dispose(); }
    }
}
