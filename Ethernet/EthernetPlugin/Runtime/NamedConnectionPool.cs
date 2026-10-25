using System.Diagnostics;
using PluginCommon;
using xTestPlatform.Core.Engine;

namespace Ethernet.Runtime;

/// <summary>One owner per execution, with generation-checked publication and real task ownership.</summary>
internal sealed class NamedConnectionPool<T> : IDisposable where T : class, IDisposable
{
    private sealed class Slot
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal long Generation;
        internal long ValueGeneration;
        internal T? Value;
        internal CancellationTokenSource? Opening;
        internal readonly HashSet<Task> Operations = new();
        internal string? RetirementKeyOverride;
        internal bool RemoveAfterRetirement;
    }

    // Immutable close generation: a delayed waiter must never retry Close(name) against a newer owner.
    private sealed class Retirement(Slot slot, long generation)
    {
        internal readonly Slot Slot = slot;
        internal readonly long Generation = generation;
        internal readonly object Sync = new();
        internal Task? Actual;
        internal volatile bool Released;
    }

    private readonly object _sync = new();
    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);
    private bool _disposed;

    internal static NamedConnectionPool<T> For(IResourceRegistry? resources, string key,
        NamedConnectionPool<T> legacy)
    {
        if (resources is null) return legacy;
        if (resources.TryGet<NamedConnectionPool<T>>(key, out var existing)) return existing;
        var created = new NamedConnectionPool<T>();
        if (resources.TryAdd(key, created)) return created;
        created.Dispose();
        if (resources.TryGet<NamedConnectionPool<T>>(key, out existing)) return existing;
        throw new InvalidOperationException("当前运行环境不支持执行级连接资源登记，不能安全持有网络连接。");
    }

    internal async Task<T> OpenAsync(string name, Func<CancellationToken, Task<T>> factory,
        int timeoutMs, CancellationToken ct, Action? ensureExecution = null,
        string? retirementKey = null, bool removeAfterRetirement = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        ensureExecution?.Invoke();
        var budget = Stopwatch.StartNew();
        await RetiringResources.WaitForPreviousAsync(retirementKey ?? RetirementKey(name), timeoutMs, ct).ConfigureAwait(false);
        int remaining = timeoutMs - (int)Math.Min(int.MaxValue, budget.ElapsedMilliseconds);
        if (remaining <= 0) throw new TimeoutException($"连接建立总期限 {timeoutMs} ms 已到（包括旧执行收尾）");
        Slot slot;
        long generation;
        CancellationTokenSource? previous;
        var deadline = new CancellationTokenSource(remaining);
        var source = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        lock (_sync)
        {
            if (_disposed) { source.Dispose(); deadline.Dispose(); throw new ObjectDisposedException(GetType().Name); }
            if (!_slots.TryGetValue(name, out slot!)) _slots.Add(name, slot = new Slot());
            if (slot.RetirementKeyOverride != retirementKey || slot.RemoveAfterRetirement != removeAfterRetirement)
            {
                if (slot.Generation != 0) throw new InvalidOperationException("同一连接名称的收尾标识不能在打开期间改变");
                slot.RetirementKeyOverride = retirementKey;
                slot.RemoveAfterRetirement = removeAfterRetirement;
            }
            generation = ++slot.Generation;
            previous = slot.Opening;
            slot.Opening = source;
        }
        Cancel(previous);
        void CheckOpeningDeadline()
        {
            ct.ThrowIfCancellationRequested();
            ensureExecution?.Invoke();
            if (budget.ElapsedMilliseconds >= timeoutMs || deadline.IsCancellationRequested)
                throw new TimeoutException($"连接建立已超过单调总期限 {timeoutMs} ms，不登记或提交迟到连接");
        }
        var actual = Task.Run(async () =>
        {
            T? staged = null;
            var entered = false;
            try
            {
                CheckOpeningDeadline();
                await slot.Gate.WaitAsync(source.Token).ConfigureAwait(false);
                entered = true;
                CheckOpeningDeadline();
                source.Token.ThrowIfCancellationRequested();
                lock (_sync)
                    if (_disposed || slot.Generation != generation)
                        throw new OperationCanceledException("连接建立已被新的打开或关闭操作取代。");

                // Only this slot's lifecycle worker may release the old instance.
                var old = slot.Value;
                if (old is not null)
                {
                    old.Dispose();
                    lock (_sync) if (ReferenceEquals(slot.Value, old)) slot.Value = null;
                }
                CheckOpeningDeadline();
                staged = await factory(source.Token).ConfigureAwait(false);
                CheckOpeningDeadline();
                source.Token.ThrowIfCancellationRequested();
                lock (_sync)
                {
                    CheckOpeningDeadline();
                    if (_disposed || slot.Generation != generation)
                        throw new OperationCanceledException("连接建立完成，但所属代次已关闭或被取代，拒绝迟到登记。");
                    slot.Value = staged;
                    slot.ValueGeneration = generation;
                }
                var result = staged;
                staged = null;
                return result;
            }
            finally
            {
                try
                {
                    if (staged != null)
                    {
                        // A late factory result is still an owned handle, even if its cleanup throws.
                        lock (_sync) { slot.Value = staged; slot.ValueGeneration = generation; }
                        staged.Dispose();
                        lock (_sync) if (ReferenceEquals(slot.Value, staged)) slot.Value = null;
                    }
                }
                finally
                {
                    lock (_sync) if (ReferenceEquals(slot.Opening, source)) slot.Opening = null;
                    source.Dispose();
                    deadline.Dispose();
                    if (entered) slot.Gate.Release();
                }
            }
        });
        Track(slot, actual);
        try
        {
            var result = await actual.WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false);
            CheckOpeningDeadline();
            lock (_sync)
                if (_disposed || slot.Generation != generation)
                    throw new InvalidOperationException("连接建立结果所属代次已关闭或被取代，拒绝迟到报告成功");
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        { Cancel(source); _ = BeginClose(name, generation); throw; }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            Cancel(source); _ = BeginClose(name, generation);
            throw new TimeoutException($"连接建立超过总期限 {timeoutMs} ms；原任务仍受生命周期管理。", ex);
        }
        catch (TimeoutException)
        { Cancel(source); _ = BeginClose(name, generation); throw; }
    }

    internal T Get(string name)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_slots.TryGetValue(name, out var slot) && slot.Value is not null &&
                slot.ValueGeneration == slot.Generation) return slot.Value;
        }
        throw new InvalidOperationException($"连接 [{name}] 不存在、正在关闭或被新一代连接取代，请先重新打开。");
    }

    internal Task CloseAsync(string name, int timeoutMs, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        return BeginClose(name).WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);
    }

    // A transient step owns one captured handle. Its delayed finally cannot retire a replacement.
    internal void RequestCloseOwned(string name, T expected)
    {
        long generation;
        lock (_sync)
        {
            if (!_slots.TryGetValue(name, out var slot) || !ReferenceEquals(slot.Value, expected) ||
                slot.Generation != slot.ValueGeneration) return;
            generation = slot.ValueGeneration;
        }
        _ = BeginClose(name, generation);
    }

    private Task BeginClose(string name, long? expectedGeneration = null)
    {
        Slot slot;
        Retirement retirement;
        CancellationTokenSource? opening;
        lock (_sync)
        {
            if (!_slots.TryGetValue(name, out slot!)) return Task.CompletedTask;
            if (expectedGeneration.HasValue && slot.Generation != expectedGeneration.Value) return Task.CompletedTask;
            retirement = new(slot, ++slot.Generation);
            opening = slot.Opening;
            // Register the captured close while holding the lifecycle lock. Do not publish
            // an intermediate state with a changed generation but no tracked retirement.
            RetiringResources.Track(slot.RetirementKeyOverride ?? RetirementKey(name), retirement,
                (limit, token) => StartRetirement(name, retirement).WaitAsync(TimeSpan.FromMilliseconds(limit), token),
                () => retirement.Released,
                () => $"网络连接={name}; 关闭代次={retirement.Generation}; 旧执行已退出={_disposed}");
        }
        Cancel(opening);
        return StartRetirement(name, retirement);
    }

    private Task StartRetirement(string name, Retirement retirement)
    {
        lock (retirement.Sync)
        {
            if (retirement.Released) return Task.CompletedTask;
            if (retirement.Actual is { IsCompleted: false }) return retirement.Actual;
            retirement.Actual = RetireCoreAsync(name, retirement);
            Track(retirement.Slot, retirement.Actual);
            return retirement.Actual;
        }
    }

    private Task RetireCoreAsync(string name, Retirement retirement)
    {
        var slot = retirement.Slot;
        var actual = Task.Run(async () =>
        {
            // A caller's timeout does not release this gate or abandon a native factory.
            await slot.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                T? current;
                lock (_sync) current = slot.ValueGeneration <= retirement.Generation ? slot.Value : null;
                if (current is not null)
                {
                    current.Dispose(); // Failed release retains the instance for an explicit retry.
                    lock (_sync) if (ReferenceEquals(slot.Value, current)) slot.Value = null;
                }
                // Acquiring the gate proves the captured factory has exited. A newer
                // generation may already be active; that is not an unfinished old close.
                retirement.Released = true;
                lock (_sync)
                    if (slot.RemoveAfterRetirement && slot.Generation == retirement.Generation &&
                        slot.Value is null && slot.Opening is null &&
                        _slots.TryGetValue(name, out var registered) && ReferenceEquals(registered, slot))
                        _slots.Remove(name);
            }
            finally { slot.Gate.Release(); }
            RetiringResources.ForgetReleased(slot.RetirementKeyOverride ?? RetirementKey(name), retirement);
        });
        return actual;
    }

    private static string RetirementKey(string name) => $"Ethernet:{typeof(T).FullName}:{name}";

    private void Track(Slot slot, Task actual)
    {
        lock (_sync) slot.Operations.Add(actual);
        _ = actual.ContinueWith(task =>
        {
            if (task.Exception is not null) Trace.TraceError($"网络连接后台操作失败: {task.Exception}");
            lock (_sync) slot.Operations.Remove(task);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static void Cancel(CancellationTokenSource? source)
    {
        if (source is null) return;
        try
        {
            _ = source.CancelAsync().ContinueWith(task =>
            {
                if (task.Exception is not null) Trace.TraceError($"网络连接取消回调失败: {task.Exception}");
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        catch (ObjectDisposedException) { /* The worker has already exited and disposed its token. */ }
    }

    public void Dispose()
    {
        string[] names;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            names = _slots.Keys.ToArray();
        }
        foreach (var name in names) _ = BeginClose(name);
    }
}
