using System.Runtime.CompilerServices;

namespace CAN.Dbc;

/// <summary>One immutable directory per content fingerprint across editors, validators and executions.</summary>
public static class DbcSharedCatalog
{
    private static readonly DbcCatalogStore Store = new();

    public static Task<(DbcDatabase Database, string Fingerprint)> LoadAsync(
        DbcDocumentSetting document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Store.LoadAsync(document.Text, ct);
    }
}

internal sealed class DbcCatalogStore
{
    private sealed record Digest(string Value);
    private int _addsSinceSweep;
    private readonly object _gate = new();
    private readonly Dictionary<string, WeakReference<DbcDatabase>> _byContent = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<string, Digest> _knownHashes = new();
    private readonly DbcConcurrentWork<string, string> _hashes = new(2, ReferenceEqualityComparer.Instance);
    private readonly DbcConcurrentWork<string, DbcDatabase> _parses = new(2);
    private readonly Func<string, CancellationToken, DbcDatabase> _parse;

    internal DbcCatalogStore(Func<string, CancellationToken, DbcDatabase>? parse = null)
        => _parse = parse ?? DbcDatabase.ParseCatalog;

    internal async Task<(DbcDatabase Database, string Fingerprint)> LoadAsync(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(text);
        string key = _knownHashes.TryGetValue(text, out var known) ? known.Value
            : await _hashes.RunAsync(text, token =>
            {
                if (_knownHashes.TryGetValue(text, out var saved)) return Task.FromResult(saved.Value);
                var digest = new Digest(DbcDocumentIO.FingerprintText(text, token));
                token.ThrowIfCancellationRequested();
                return Task.FromResult(_knownHashes.GetValue(text, _ => digest).Value);
            }, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (Find(key) is { } existing) return (existing, key);
        var result = await _parses.RunAsync(key, token =>
        {
            // Another load may have completed between the initial lookup and admission.
            if (Find(key) is { } cached) return Task.FromResult(cached);
            var parsed = _parse(text, token);
            lock (_gate)
            {
                token.ThrowIfCancellationRequested();
                // Live owners keep their identity even when >128 distinct documents are open.
                // Only weak entries are retained. Sweep expiring metadata in bounded intervals.
                if (++_addsSinceSweep >= 64)
                {
                    foreach (var dead in _byContent.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToArray())
                        _byContent.Remove(dead);
                    _addsSinceSweep = 0;
                }
                _byContent[key] = new(parsed);
            }
            return Task.FromResult(parsed);
        }, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return (result, key);
    }

    private DbcDatabase? Find(string key)
    {
        lock (_gate)
            return _byContent.TryGetValue(key, out var reference) && reference.TryGetTarget(out var value) ? value : null;
    }
}

/// <summary>Shares only overlapping work. The last departing waiter cancels the worker.
/// Completed reads and faults are never reused by a later request.</summary>
internal sealed class DbcConcurrentWork<TKey, TResult>(int parallelism, IEqualityComparer<TKey>? comparer = null,
    int maxOutstanding = 128) where TKey : notnull
{
    private sealed class Job
    {
        internal readonly CancellationTokenSource Stop = new();
        internal Task<TResult> Task = null!;
        internal int Waiters;
        internal Task Cancelled = System.Threading.Tasks.Task.CompletedTask;
    }

    private readonly object _gate = new();
    private readonly Dictionary<TKey, Job> _jobs = new(comparer);
    private readonly SemaphoreSlim _slots = new(parallelism, parallelism);
    private int _outstanding;

    internal async Task<TResult> RunAsync(TKey key, Func<CancellationToken, Task<TResult>> work, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        Job job;
        lock (_gate)
        {
            if (!_jobs.TryGetValue(key, out job!))
            {
                if (_outstanding >= maxOutstanding)
                    throw new InvalidOperationException($"尚未退出的 DBC 工作数已达上限 {maxOutstanding}");
                job = new();
                _outstanding++;
                _jobs.Add(key, job);
                if (ExecutionContext.IsFlowSuppressed()) Start(key, job, work);
                else { using (ExecutionContext.SuppressFlow()) Start(key, job, work); }
            }
            job.Waiters++;
        }
        try
        {
            var result = await job.Task.WaitAsync(caller).ConfigureAwait(false);
            caller.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            lock (_gate)
            {
                job.Waiters--;
                if (job.Waiters == 0 && RemoveCurrent(key, job))
                    job.Cancelled = job.Stop.CancelAsync();
            }
        }
    }

    private void Start(TKey key, Job job, Func<CancellationToken, Task<TResult>> work)
    {
        // The process-wide reader must never inherit a sequence's AsyncLocal context.
        job.Task = Task.Run(() => RunWorkerAsync(key, job, work));
        _ = job.Task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task<TResult> RunWorkerAsync(TKey key, Job job, Func<CancellationToken, Task<TResult>> work)
    {
        try
        {
            var token = job.Stop.Token;
            await _slots.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                return await work(token).ConfigureAwait(false);
            }
            finally { _slots.Release(); }
        }
        finally
        {
            lock (_gate) RemoveCurrent(key, job);
            try { await job.Cancelled.ConfigureAwait(false); }
            finally
            {
                job.Stop.Dispose();
                lock (_gate) _outstanding--;
            }
        }
    }

    // Both callers hold _gate. A cancelled worker must never remove its replacement.
    private bool RemoveCurrent(TKey key, Job job)
        => _jobs.TryGetValue(key, out var current) && ReferenceEquals(current, job) && _jobs.Remove(key);
}
