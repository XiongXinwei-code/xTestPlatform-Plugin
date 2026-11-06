using System.IO;

namespace LabVIEWCallPlugin.Execution;

// File.Exists can block in the Windows network redirector. Probes access only the path,
// hold no VI ticket and publish nothing. Concurrent callers share an outstanding probe.
internal sealed class ViFileProbe(Func<string, bool> fileExists)
{
    internal static ViFileProbe Shared { get; } = new(File.Exists);
    private readonly Dictionary<string, Task<bool>> _active = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _slots = new(4);

    internal async Task<bool> ExistsAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Task<bool>? actual;
        lock (_active) _active.TryGetValue(path, out actual);
        if (actual == null)
        {
            await _slots.WaitAsync(token).ConfigureAwait(false);
            lock (_active)
            {
                if (_active.TryGetValue(path, out actual))
                    _slots.Release();
                else
                {
                    Task<bool> Start()
                    {
                        var worker = Task.Run(() =>
                        {
                            try { return fileExists(path); }
                            finally
                            {
                                lock (_active) _active.Remove(path);
                                _slots.Release();
                            }
                        });
                        _ = worker.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        return worker;
                    }
                    // A stalled UNC provider must not retain the host's ambient execution registry.
                    if (ExecutionContext.IsFlowSuppressed()) actual = Start();
                    else { using (ExecutionContext.SuppressFlow()) actual = Start(); }
                    _active.Add(path, actual);
                }
            }
        }
        bool exists = await actual.WaitAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return exists;
    }
}
