namespace PluginCommon;

/// <summary>
/// Retires observers independently of native cancellation. This signal never owns
/// an I/O task, a driver handle or a cancellation token passed to a driver.
/// </summary>
internal sealed class OwnerStopSignal
{
    private readonly TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool IsStopRequested => _requested.Task.IsCompleted;
    internal Task StopRequested => _requested.Task;
    internal void RequestStop() => _requested.TrySetResult();

    internal void ThrowIfStopped(string description)
    {
        if (IsStopRequested)
            throw new InvalidOperationException($"{description}：资源已请求退出，当前步骤结束等待；真实 I/O 与释放结果仍由原所有者跟踪");
    }

    internal async Task<T> WaitAsync<T>(Task<T> actual, TimeSpan timeout, CancellationToken caller, string description)
    {
        Observe(actual); // Caller may have created a real task just before retirement won.
        caller.ThrowIfCancellationRequested();
        ThrowIfStopped(description);
        // This private token cancels only the BCL observer/timer, never actual I/O.
        using var observer = CancellationTokenSource.CreateLinkedTokenSource(caller);
        var waiting = actual.WaitAsync(timeout, observer.Token);
        try
        {
            await Task.WhenAny(waiting, _requested.Task).ConfigureAwait(false);
            caller.ThrowIfCancellationRequested();
            ThrowIfStopped(description);
            var result = await waiting.ConfigureAwait(false);
            caller.ThrowIfCancellationRequested();
            ThrowIfStopped(description);
            return result;
        }
        finally
        {
            if (!waiting.IsCompleted) observer.Cancel();
            Observe(waiting);
        }
    }

    internal Task WaitAsync(Task actual, TimeSpan timeout, CancellationToken caller, string description)
    {
        var result = AsResult(actual);
        Observe(result); // A stop already requested before entry must not orphan this adapter task.
        return WaitAsync(result, timeout, caller, description);
    }

    internal async Task<bool> EnterAsync(SemaphoreSlim gate, int timeoutMs, CancellationToken caller, string description)
    {
        caller.ThrowIfCancellationRequested();
        ThrowIfStopped(description);
        using var admission = CancellationTokenSource.CreateLinkedTokenSource(caller);
        var acquired = gate.WaitAsync(timeoutMs, admission.Token);
        try
        {
            await Task.WhenAny(acquired, _requested.Task).ConfigureAwait(false);
            caller.ThrowIfCancellationRequested();
            ThrowIfStopped(description);
            bool entered = await acquired.ConfigureAwait(false);
            caller.ThrowIfCancellationRequested();
            ThrowIfStopped(description);
            return entered;
        }
        catch
        {
            // Remove a retired queued waiter; if admission raced with retirement,
            // return precisely its acquired permit. Never release an active I/O gate.
            admission.Cancel();
            try { if (await acquired.ConfigureAwait(false)) gate.Release(); }
            catch (OperationCanceledException) { }
            throw;
        }
    }

    private static async Task<bool> AsResult(Task task) { await task.ConfigureAwait(false); return true; }
    private static void Observe(Task task) => _ = task.ContinueWith(t => { _ = t.Exception; },
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
