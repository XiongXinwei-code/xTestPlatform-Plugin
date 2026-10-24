using System.Diagnostics;

namespace PluginCommon;

/// <summary>
/// Keeps already-closing owners reachable after an execution registry has been disposed.
/// This is not an active-connection lock and never closes another active connection.
/// Compiled privately into each plugin. SDK release and actual task exit remain the owner's responsibility.
/// </summary>
internal static class RetiringResources
{
    private sealed class Entry(object owner, Func<int, CancellationToken, Task> retryClose,
        Func<bool> isReleased, Func<string>? diagnostics)
    {
        internal readonly object Owner = owner;
        internal readonly Func<bool> IsReleased = isReleased;
        internal readonly Func<string>? Diagnostics = diagnostics;
        private readonly object _sync = new();
        private Task? _attempt;

        internal Task Retry(int timeoutMs)
        {
            lock (_sync)
            {
                if (_attempt == null || _attempt.IsCompleted)
                {
                    // The next run can stop waiting, but must not cancel the previous owner's cleanup.
                    _attempt = Task.Run(async () => await retryClose(timeoutMs, CancellationToken.None).ConfigureAwait(false));
                    _ = _attempt.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                return _attempt;
            }
        }
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<string, List<Entry>> Entries = new(StringComparer.OrdinalIgnoreCase);

    internal static void Track(string key, object owner, Func<int, CancellationToken, Task> retryClose,
        Func<bool> isReleased, Func<string>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(retryClose); ArgumentNullException.ThrowIfNull(isReleased);
        lock (Sync)
        {
            if (!Entries.TryGetValue(key, out var entries)) Entries.Add(key, entries = []);
            if (entries.Any(e => ReferenceEquals(e.Owner, owner))) return;
            entries.Add(new Entry(owner, retryClose, isReleased, diagnostics));
        }
    }

    /// <summary>Called by a successful close finally block; never removes a different owner's retirement.</summary>
    internal static bool ForgetReleased(string key, object owner)
    {
        Entry? entry;
        lock (Sync) entry = Entries.TryGetValue(key, out var entries) ? entries.Find(e => ReferenceEquals(e.Owner, owner)) : null;
        if (entry == null || !entry.IsReleased()) return false;
        Remove(key, entry);
        return true;
    }

    internal static async Task WaitForPreviousAsync(string key, int timeoutMs, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested(); var watch = Stopwatch.StartNew();
        while (true)
        {
            Entry[] pending;
            lock (Sync) pending = Entries.TryGetValue(key, out var entries) ? entries.ToArray() : [];
            if (pending.Length == 0) return;
            foreach (var entry in pending)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.IsReleased()) { Remove(key, entry); continue; }
                int remaining = timeoutMs - (int)Math.Min(int.MaxValue, watch.ElapsedMilliseconds);
                if (remaining <= 0) throw Deadline(key, entry, timeoutMs);
                Task actual = entry.Retry(remaining);
                try { await actual.WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (TimeoutException ex)
                {
                    if (entry.IsReleased())
                    {
                        Trace.TraceWarning($"旧资源 {key} 已确认释放，保留历史关闭诊断但不阻止新运行：{ex}");
                        Remove(key, entry);
                        continue;
                    }
                    throw Deadline(key, entry, timeoutMs, ex);
                }
                catch (Exception ex)
                {
                    if (entry.IsReleased())
                    {
                        Trace.TraceWarning($"旧资源 {key} 已确认释放，保留历史关闭诊断但不阻止新运行：{ex}");
                        Remove(key, entry);
                        continue;
                    }
                    throw new InvalidOperationException($"旧资源 {key} 收尾失败；未确认释放的所有者仍保留，可重试关闭。{Describe(entry)}", ex);
                }
                if (!entry.IsReleased())
                    throw new InvalidOperationException($"旧资源 {key} 的关闭等待已返回，但未确认释放；不能建立新资源。{Describe(entry)}");
                Remove(key, entry);
            }
        }
    }

    private static TimeoutException Deadline(string key, Entry entry, int timeoutMs, Exception? inner = null) =>
        new($"等待旧资源 {key} 收尾超过总期限 {timeoutMs} ms；真实任务/驱动尚未确认退出，保留所有者与隔离。{Describe(entry)}", inner);

    private static string Describe(Entry entry)
    {
        try { return entry.Diagnostics?.Invoke() ?? ""; }
        catch (Exception ex) { Trace.TraceError($"读取收尾诊断失败：{ex}"); return $"诊断回调失败：{ex.GetType().Name}"; }
    }

    private static void Remove(string key, Entry entry)
    {
        lock (Sync)
        {
            if (!Entries.TryGetValue(key, out var entries)) return;
            entries.Remove(entry);
            if (entries.Count == 0) Entries.Remove(key);
        }
    }
}
