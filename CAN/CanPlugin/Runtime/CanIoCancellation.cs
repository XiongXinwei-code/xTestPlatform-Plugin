namespace CAN.Runtime;

/// <summary>原调用方只请求异步取消，真实 I/O 同时拥有取消回调的退出。</summary>
internal sealed class CanIoCancellation : IAsyncDisposable
{
    private readonly object _state = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly CancellationTokenRegistration _caller, _owner;
    private readonly Timer _timer;
    private Task _callbacks = Task.CompletedTask;
    private bool _sealed;
    internal CancellationToken Token { get; }
    internal bool IsCancellationRequested => Token.IsCancellationRequested;
    internal CanIoCancellation(CancellationToken caller, CancellationToken owner, int remainingMs)
    {
        Token = _cancel.Token;
        _caller = caller.UnsafeRegister(static state => ((CanIoCancellation)state!).RequestCancellation(), this);
        _owner = owner.UnsafeRegister(static state => ((CanIoCancellation)state!).RequestCancellation(), this);
        _timer = new Timer(static state => ((CanIoCancellation)state!).RequestCancellation(), this, remainingMs, Timeout.Infinite);
    }
    private void RequestCancellation()
    {
        lock (_state)
            if (!_sealed && !_cancel.IsCancellationRequested) _callbacks = _cancel.CancelAsync();
    }
    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync().ConfigureAwait(false);
        await _caller.DisposeAsync().ConfigureAwait(false);
        await _owner.DisposeAsync().ConfigureAwait(false);
        Task callbacks;
        lock (_state) { _sealed = true; callbacks = _callbacks; }
        try { await callbacks.ConfigureAwait(false); }
        finally { _cancel.Dispose(); }
    }
}
