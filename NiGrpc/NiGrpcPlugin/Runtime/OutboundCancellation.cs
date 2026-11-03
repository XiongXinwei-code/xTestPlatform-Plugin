using System.Diagnostics;

namespace NiGrpc.Runtime;

/// <summary>取消状态立即转发，外部回调和真实调用退出均由后台观察。</summary>
internal sealed class OutboundCancellation
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _source = new();
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _callerRegistration;
    private readonly Task? _scopeWatcher;
    private Task? _firstCancellation;
    private bool _closed;
    internal CancellationToken Token => _source.Token;

    internal OutboundCancellation(CancellationToken caller, Task? scopeStop = null)
    {
        _callerRegistration = caller.UnsafeRegister(static state => ((OutboundCancellation)state!).RequestCancel(), this);
        if (scopeStop != null) _scopeWatcher = WatchScopeAsync(scopeStop);
    }

    internal void RequestCancel()
    {
        lock (_sync)
        {
            if (_closed || _firstCancellation != null) return;
            try { _firstCancellation = _source.CancelAsync(); }
            catch (Exception ex) { _firstCancellation = Task.FromException(ex); }
        }
    }

    private async Task WatchScopeAsync(Task scopeStop)
    {
        var winner = await Task.WhenAny(scopeStop, _finished.Task).ConfigureAwait(false);
        if (winner == scopeStop) RequestCancel();
    }

    internal async Task FinishAsync(Task? actual)
    {
        if (actual != null) try { await actual.ConfigureAwait(false); } catch { /* 原调用由调用方或所有者记录。 */ }
        _finished.TrySetResult();
        await _callerRegistration.DisposeAsync().ConfigureAwait(false);
        if (_scopeWatcher != null) await _scopeWatcher.ConfigureAwait(false);
        Task? cancellation;
        lock (_sync) { _closed = true; cancellation = _firstCancellation; }
        if (cancellation != null)
            try { await cancellation.ConfigureAwait(false); }
            catch (Exception ex) { Trace.TraceError($"NI gRPC 取消回调异常：{ex}"); }
        _source.Dispose();
    }
}
