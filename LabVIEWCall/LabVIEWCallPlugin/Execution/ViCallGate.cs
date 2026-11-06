namespace LabVIEWCallPlugin.Execution;

/// <summary>The bundled bridge exposes a single local endpoint, not identifiable VI clones.
/// A waiter can return early. Only the actual worker may surrender its ticket.</summary>
internal sealed class ViCallGate
{
    private sealed record Ticket(string Operation, TaskCompletionSource Exited);

    private static readonly ViCallGate Shared = new();
    private readonly object _sync = new();
    private Ticket? _active;

    internal static Task SharedIdle => Shared.WhenIdle;
    internal Task WhenIdle { get { lock (_sync) return _active?.Exited.Task ?? Task.CompletedTask; } }

    internal static Task<T> RunAsync<T>(string operation, int timeoutMs, CancellationToken cancellationToken,
        Func<CancellationToken, T> work) => Shared.InvokeAsync(operation, timeoutMs, cancellationToken, work);

    internal static Task<T> RunStepAsync<T>(string operation, CancellationToken cancellationToken,
        Func<CancellationToken, Task<T>> work) => Shared.InvokeWorkerAsync(operation, 0, cancellationToken, work);

    internal Task<T> InvokeAsync<T>(string operation, int timeoutMs, CancellationToken cancellationToken,
        Func<CancellationToken, T> work) =>
        InvokeWorkerAsync(operation, timeoutMs, cancellationToken, token => Task.FromResult(work(token)));

    private async Task<T> InvokeWorkerAsync<T>(string operation, int timeoutMs, CancellationToken cancellationToken,
        Func<CancellationToken, Task<T>> work)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs > 0) deadline.CancelAfter(timeoutMs);
        var stop = deadline.Token;
        var ticket = new Ticket(operation, new(TaskCreationOptions.RunContinuationsAsynchronously));
        lock (_sync)
        {
            if (_active != null)
                throw new InvalidOperationException($"LabVIEW 桥正在执行或等待旧调用退出：{_active.Operation}。本次操作 {operation} 未提交。");
            _active = ticket;
        }
        Task<T> actual;
        try
        {
            actual = Task.Run(async () =>
            {
                try
                {
                    stop.ThrowIfCancellationRequested();
                    return await work(stop).ConfigureAwait(false);
                }
                finally { Release(ticket); }
            });
        }
        catch { Release(ticket); throw; }

        // Observe a native failure even if the caller has already stopped waiting.
        _ = actual.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            T result = await actual.WaitAsync(stop).ConfigureAwait(false);
            stop.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && stop.IsCancellationRequested)
        {
            throw new TimeoutException($"LabVIEW 操作 {operation} 等待超过 {timeoutMs} ms；已提交的原生调用可能仍在执行，直到实际返回才允许再次提交。", ex);
        }
    }

    private void Release(Ticket ticket)
    {
        lock (_sync)
        {
            _active = null;
            ticket.Exited.TrySetResult();
        }
    }
}
