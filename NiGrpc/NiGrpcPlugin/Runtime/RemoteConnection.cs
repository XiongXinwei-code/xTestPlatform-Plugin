using System.Diagnostics;

namespace NiGrpc.Runtime;

/// <summary>一个平台命名连接拥有独立通道和本连接创建的远端会话。</summary>
internal sealed class RemoteConnection : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Task<RemoteSession>> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<Task<RemoteSession>, TaskCompletionSource<Task>> _openFinishes = [];
    private readonly HashSet<Task> _openDrains = [];
    private readonly IRemoteWire _wire;
    private readonly IDisposable _channel;
    private readonly int _cleanupTimeoutMs;
    private readonly Action<Exception>? _report;
    private readonly Action<Task>? _track;
    private readonly Action<string>? _unknown;
    private bool _closing;
    private Task? _close;
    private readonly TaskCompletionSource _closeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _closeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task CloseCompletion => _closeCompletion.Task;
    internal Task CloseStarted => _closeStarted.Task;
    internal bool LocalChannelClosed { get; private set; }
    internal bool IsClosing { get { lock (_sync) return _closing; } }

    internal RemoteConnection(IRemoteWire wire, IDisposable channel, int cleanupTimeoutMs, Action<Exception>? report = null, Action<Task>? track = null, Action<string>? unknown = null)
    { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cleanupTimeoutMs); _wire = wire; _channel = channel; _cleanupTimeoutMs = cleanupTimeoutMs; _report = report; _track = track; _unknown = unknown; _ = _closeCompletion.Task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted); }

    internal async Task<RemoteSession> OpenSessionAsync(string alias, NiDriver driver, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null, Task? scopeStop = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ct.ThrowIfCancellationRequested();
        var slot = new TaskCompletionSource<RemoteSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var openFinish = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? trackedOpen = null;
        bool createdAndOwned = false;
        lock (_sync)
        {
            if (_closing) throw new InvalidOperationException("NI gRPC 连接正在关闭");
            if (!_sessions.TryAdd(alias, slot.Task)) throw new InvalidOperationException($"NI gRPC 会话 {alias} 已存在或正在建立");
            _openFinishes.Add(slot.Task, openFinish);
            var drain = openFinish.Task.Unwrap();
            _openDrains.Add(drain);
            _ = drain.ContinueWith(t =>
            {
                if (t.IsFaulted) _ = t.Exception;
                lock (_sync) if (t.IsCompletedSuccessfully) _openDrains.Remove(drain);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        _ = slot.Task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        try
        {
            var session = await RemoteSession.OpenAsync(_wire, driver, json, timeoutMs, ct, () =>
            {
                checkAdmission?.Invoke();
                lock (_sync) if (_closing) throw new InvalidOperationException("连接正在关闭，禁止新建远端会话");
            }, scopeStop, task => { trackedOpen = task; _track?.Invoke(task); }, _unknown).ConfigureAwait(false);
            slot.TrySetResult(session);
            createdAndOwned = true;
            lock (_sync)
                if (_closing) throw new InvalidOperationException($"连接已关闭或正在关闭；远端会话 {session.RemoteName} 已交由连接收尾，不能报告创建成功");
            return session;
        }
        catch (LocalInputException ex)
        {
            slot.TrySetException(ex);
            lock (_sync)
                if (_sessions.TryGetValue(alias, out var current) && ReferenceEquals(current, slot.Task)) { _sessions.Remove(alias); _openFinishes.Remove(slot.Task); }
            throw;
        }
        catch (NotSubmittedCanceledException ex)
        {
            slot.TrySetException(ex);
            lock (_sync)
                if (_sessions.TryGetValue(alias, out var current) && ReferenceEquals(current, slot.Task)) { _sessions.Remove(alias); _openFinishes.Remove(slot.Task); }
            throw;
        }
        catch (Exception ex) { if (!createdAndOwned) _unknown?.Invoke($"会话 {alias} 的创建结果未知：{ex.Message}"); slot.TrySetException(ex); throw; }
        finally { openFinish.TrySetResult(trackedOpen ?? Task.CompletedTask); }
    }

    internal async Task<RemoteSession> GetSessionAsync(string alias, CancellationToken ct)
    {
        Task<RemoteSession> session;
        lock (_sync)
        {
            if (_closing) throw new InvalidOperationException("NI gRPC 连接正在关闭");
            if (!_sessions.TryGetValue(alias, out session!)) throw new InvalidOperationException($"NI gRPC 会话 {alias} 不存在");
        }
        return await session.WaitAsync(ct).ConfigureAwait(false);
    }

    internal async Task CloseSessionAsync(string alias, int timeoutMs, CancellationToken ct, Action? checkAdmission = null, Task? scopeStop = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        var clock = Stopwatch.StartNew();
        var session = await GetSessionAsync(alias, ct).WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
        try { checkAdmission?.Invoke(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new LocalInputException("执行作用域已退出，Clear 未提交", ex); }
        int remaining = clock.ElapsedMilliseconds < timeoutMs ? timeoutMs - (int)clock.ElapsedMilliseconds
            : throw new TimeoutException($"NI gRPC 会话关闭等待超过 {timeoutMs} ms，未提交 Clear");
        await CloseSessionIfSameAsync(alias, session, remaining, ct, checkAdmission, scopeStop).ConfigureAwait(false);
    }

    internal async Task CloseSessionIfSameAsync(string alias, RemoteSession session, int timeoutMs, CancellationToken ct, Action? checkAdmission = null, Task? scopeStop = null)
    {
        lock (_sync)
            if (!_sessions.TryGetValue(alias, out var expected) || !expected.IsCompletedSuccessfully || !ReferenceEquals(expected.Result, session))
                throw new InvalidOperationException("NI gRPC 会话代次已改变，不能关闭后续会话");
        await session.CloseAsync(timeoutMs, ct, checkAdmission, scopeStop).ConfigureAwait(false);
        lock (_sync)
            if (_sessions.TryGetValue(alias, out var existing) && existing.IsCompletedSuccessfully && ReferenceEquals(existing.Result, session))
            { _sessions.Remove(alias); _openFinishes.Remove(existing); }
    }

    internal async Task CloseAsync(int timeoutMs, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        Task actual = RequestClose(timeoutMs);
        await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
    }

    internal Task RequestClose(int timeoutMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        lock (_sync)
        {
            _closing = true;
            _closeStarted.TrySetResult();
            if (_close != null) return _close;
            var actual = _close = Task.Run(() => CloseCoreAsync(timeoutMs));
            _ = actual.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully) _closeCompletion.TrySetResult();
                else if (t.IsCanceled) _closeCompletion.TrySetCanceled();
                else _closeCompletion.TrySetException(t.Exception!.InnerExceptions);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            _ = actual.ContinueWith(t =>
            {
                if (t.Exception is not { } error) return;
                try { _report?.Invoke(error); } catch { Trace.TraceError(error.ToString()); }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return actual;
        }
    }

    private async Task CloseCoreAsync(int timeoutMs)
    {
        (string Alias, Task<RemoteSession> Slot, Task<Task> OpenFinish)[] slots;
        Task[] openDrains;
        lock (_sync) { slots = _sessions.Select(e => (e.Key, e.Value, _openFinishes[e.Value].Task)).ToArray(); openDrains = _openDrains.ToArray(); }
        var failures = new List<Exception>();
        try
        {
            var slotResults = Task.WhenAll(slots.Select(CloseSlotAsync));
            var drainResults = Task.WhenAll(openDrains.Select(async drain =>
            {
                try { await drain.ConfigureAwait(false); return (Exception?)null; }
                catch (Exception ex) { return new InvalidOperationException("创建会话的本地收尾失败", ex); }
            }));
            failures.AddRange((await slotResults.ConfigureAwait(false)).OfType<Exception>());
            failures.AddRange((await drainResults.ConfigureAwait(false)).OfType<Exception>());
        }
        finally
        {
            try { _channel.Dispose(); LocalChannelClosed = true; }
            catch (Exception ex) { failures.Add(new InvalidOperationException("本地 gRPC 通道关闭失败，保留连接所有权", ex)); }
        }
        if (failures.Count > 0)
        {
            if (failures.Any(e => e is not ConfirmedCleanupPendingException))
                _unknown?.Invoke("连接收尾失败，旧会话远端状态或本地通道未确认");
            throw new AggregateException(LocalChannelClosed
                ? "本地 gRPC 通道已关闭，但部分会话收尾未完成；保留远端名称与原始错误"
                : "本地 gRPC 通道关闭未完成；保留连接所有权及远端释放的原始错误", failures);
        }

        async Task<Exception?> CloseSlotAsync((string Alias, Task<RemoteSession> Slot, Task<Task> OpenFinish) entry)
        {
            var slot = entry.Slot;
            try
            {
                // 建立过程仍在运行时必须保留通道和所有权；前台等待仍由 CloseAsync 的期限控制。
                RemoteSession session;
                try { session = await slot.ConfigureAwait(false); }
                finally { await (await entry.OpenFinish.ConfigureAwait(false)).ConfigureAwait(false); }
                while (true)
                {
                    try { await session.CloseAsync(timeoutMs, CancellationToken.None).ConfigureAwait(false); return null; }
                    catch (CloseQueueTimeoutException) { await session.WaitForIdleAsync().ConfigureAwait(false); }
                    catch (TimeoutException) when (session.ClearConfirmed) { await session.WaitForIdleAsync().ConfigureAwait(false); }
                }
            }
            catch (LocalInputException) { return null; }
            catch (NotSubmittedCanceledException) { return null; }
            catch (Exception ex)
            {
                // 远端结果未知时仍须等待本地原调用和取消回调实际退出，再销毁通道。
                if (slot.IsCompletedSuccessfully) await slot.Result.WaitForIdleAsync().ConfigureAwait(false);
                string identity = slot.IsCompletedSuccessfully ? $"远端名称={slot.Result.RemoteName}" : "远端创建结果待确认，名称见创建错误或服务端日志";
                return slot.IsCompletedSuccessfully && slot.Result.ClearConfirmed
                    ? new ConfirmedCleanupPendingException($"会话别名={entry.Alias}；{identity}；远端 Clear 已确认，本地收尾仍需跟踪：{ex.Message}", ex)
                    : new InvalidOperationException($"会话别名={entry.Alias}；{identity}；未确认释放：{ex.Message}", ex);
            }
        }
    }

    public void Dispose()
    {
        _ = RequestClose(_cleanupTimeoutMs);
    }
}
