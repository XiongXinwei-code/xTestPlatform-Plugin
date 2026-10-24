using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Opc.Ua;
using Opc.Ua.Client;
using PluginCommon;

namespace OpcUa.Helpers;

/// <summary>连接拥有真实请求、采集任务及订阅活动；不实现自动重连。</summary>
public sealed class OpcUaConnectionResource : IDisposable
{
    private readonly object _state = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<Task> _requests = new();
    private readonly HashSet<OpcUaDataAcqTask> _collectors = new();
    private readonly HashSet<TaskCompletionSource> _activities = new();
    private readonly AsyncLocal<OpcUaRequestOperation?> _requestOperation = new();
    private readonly Func<Task> _close;
    private readonly Action<Exception>? _report;
    private Task? _closeTask;
    private bool _closing, _closed;
    private string? _isolated;
    private Session? _session;
    private bool _serverClosed;
    public Session Session => _session ?? throw new InvalidOperationException("此连接没有 OPC UA Session");
    public int DefaultTimeoutMs { get; }
    public CancellationToken LifetimeToken { get; }
    public string? IsolatedReason { get { lock (_state) return _isolated; } }
    public bool IsClosed { get { lock (_state) return _closed; } }
    public bool HasPendingOperation { get { lock (_state) return _requests.Any(t => !t.IsCompleted); } }
    public Exception? LastCloseError { get; private set; }
    public string? RetirementKey { get; }
    internal Action? ExecutionCheck { get; set; }
    internal void EnsureAvailableForPublication() { ExecutionCheck?.Invoke(); lock (_state) CheckAvailable(); }
    internal Func<ReadValueIdCollection, CancellationToken, Task<DataValueCollection>>? AcquisitionReader { get; set; }

    public OpcUaConnectionResource(Func<Task> close, int timeoutMs, Action<Exception>? report = null, string? retirementKey = null)
    {
        _close = close; DefaultTimeoutMs = timeoutMs > 0 ? timeoutMs : 5000;
        _report = report; LifetimeToken = _lifetime.Token;
        RetirementKey = retirementKey;
    }

    public OpcUaConnectionResource(Session session, int timeoutMs, Action<Exception>? report = null)
        : this(CreateClose(session), timeoutMs, report) => _session = session;

    public OpcUaConnectionResource(int timeoutMs, Action<Exception>? report = null, string? retirementKey = null)
        : this(() => Task.CompletedTask, timeoutMs, report, retirementKey) => _close = CloseAttachedSessionAsync;

    public Task InitializeAsync(Func<CancellationToken, Task<Session>> create, int timeoutMs, CancellationToken ct)
    {
        var startedAt = Stopwatch.GetTimestamp();
        return RunAsync(async token =>
        {
            if (RetirementKey != null)
                await RetiringResources.WaitForPreviousAsync(RetirementKey,
                    OpcUaHelper.RemainingBudget(startedAt, timeoutMs), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            _ = OpcUaHelper.RemainingBudget(startedAt, timeoutMs);
            ExecutionCheck?.Invoke();
            // 即使 SDK 忽略取消并迟到返回，先接管 Session，随后拒绝将关闭中的代次报告可用。
            var session = await create(token).ConfigureAwait(false);
            lock (_state)
            {
                _session = session;
                CheckAvailable();
            }
            token.ThrowIfCancellationRequested();
            _ = OpcUaHelper.RemainingBudget(startedAt, timeoutMs);
        }, timeoutMs, "OPC UA 连接", ct);
    }

    private async Task CloseAttachedSessionAsync()
    {
        if (_session == null) return;
        if (!_serverClosed)
        {
            var status = await _session.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            if (StatusCode.IsBad(status)) throw new ServiceResultException(status.Code, "OPC UA 关闭 Session 失败");
            _serverClosed = true;
        }
        _session.Dispose();
        _session = null;
    }

    private static Func<Task> CreateClose(Session session)
    {
        bool serverClosed = false;
        return async () =>
        {
            if (!serverClosed)
            {
                var status = await session.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                if (StatusCode.IsBad(status)) throw new ServiceResultException(status.Code, "OPC UA 关闭 Session 失败");
                serverClosed = true;
            }
            session.Dispose();
        };
    }

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, int timeoutMs, string description, CancellationToken ct)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "OPC UA 单次期限必须大于 0");
        ct.ThrowIfCancellationRequested();
        ExecutionCheck?.Invoke();
        // 这个等待令牌不交给 SDK，宿主取消线程不会执行 SDK 注册的任意取消回调。
        using var waiter = CancellationTokenSource.CreateLinkedTokenSource(ct, LifetimeToken);
        OpcUaRequestOperation operation;
        Task<T> actual;
        lock (_state)
        {
            CheckAvailable();
            ct.ThrowIfCancellationRequested();
            operation = new OpcUaRequestOperation(timeoutMs, description, ct, LifetimeToken);
            actual = Task.Run(ExecuteActualAsync, CancellationToken.None);
            _requests.Add(actual);
        }
        _ = actual.ContinueWith(t =>
        {
            if (t.IsFaulted) _ = t.Exception;
            lock (_state) _requests.Remove(actual);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            var result = await actual.WaitAsync(operation.Remaining, waiter.Token).ConfigureAwait(false);
            operation.ThrowIfActive();
            lock (_state) CheckAvailable();
            return result;
        }
        catch (OperationCanceledException ex)
        {
            operation.RequestCancellation();
            IsolatePending();
            if (operation.IsExpired && !ct.IsCancellationRequested && !LifetimeToken.IsCancellationRequested)
                throw Deadline(ex);
            throw;
        }
        catch (TimeoutException ex)
        {
            operation.RequestCancellation();
            IsolatePending();
            throw Deadline(ex);
        }

        void IsolatePending()
        {
            if (!actual.IsCompleted)
                lock (_state) _isolated ??= $"{description} 停止等待，但原始请求或其取消回调尚未退出，必须关闭后由序列重新连接";
        }
        TimeoutException Deadline(Exception cause) => new($"{description} 超过 {timeoutMs} ms；原始请求及取消回调已退出={actual.IsCompleted}；{IsolatedReason}", cause);

        async Task<T> ExecuteActualAsync()
        {
            var previous = _requestOperation.Value;
            _requestOperation.Value = operation;
            T result = default!;
            Exception? failure = null;
            try
            {
                ThrowIfSubmissionStopped();
                result = await action(operation.Token).ConfigureAwait(false);
                ThrowIfSubmissionStopped();
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                _requestOperation.Value = previous;
                try { await operation.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup)
                {
                    failure = failure == null ? cleanup : new AggregateException("OPC UA 原请求与取消回调均有失败", failure, cleanup);
                }
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }
    }

    /// <summary>复合事务在每次新的 SDK 提交前使用；仅可在当前请求范围内调用。</summary>
    internal void ThrowIfSubmissionStopped()
    {
        ExecutionCheck?.Invoke();
        var operation = _requestOperation.Value ?? throw new InvalidOperationException("OPC UA SDK 提交必须位于连接拥有的请求事务中");
        operation.ThrowIfActive();
        lock (_state) CheckAvailable();
    }

    public Task RunAsync(Func<CancellationToken, Task> action, int timeoutMs, string description, CancellationToken ct) =>
        RunAsync(async token => { await action(token).ConfigureAwait(false); return true; }, timeoutMs, description, ct);

    public IDisposable EnterActivity()
    {
        ExecutionCheck?.Invoke();
        lock (_state)
        {
            CheckAvailable();
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _activities.Add(finished);
            return new Activity(this, finished);
        }
    }

    public OpcUaDataAcqTask StartAcquisition(string name, List<Models.OpcUaDataAcqItem> items, int intervalMs, int durationMs, int bufferSize)
    {
        ExecutionCheck?.Invoke();
        lock (_state)
        {
            CheckAvailable();
            var nodes = new ReadValueIdCollection(items.Select(i => new ReadValueId
            { NodeId = OpcUaHelper.ParseNodeId(i.NodeId), AttributeId = Attributes.Value }));
            var task = new OpcUaDataAcqTask(name,
                token => ReadAcquisitionAsync(nodes, name, token),
                items, intervalMs, durationMs, bufferSize, LifetimeToken);
            _collectors.Add(task);
            _ = task.CloseCompletion.ContinueWith(completed =>
            {
                _ = completed.Exception;
                lock (_state) _collectors.Remove(task);
            },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return task;
        }
    }

    private async Task<DataValueCollection> ReadAcquisitionAsync(ReadValueIdCollection nodes, string name, CancellationToken caller)
    {
        // 采集循环本身就是长期 owner；不能再包一层提前返回的软等待，使 Stop 只等到软 waiter。
        var operation = new OpcUaRequestOperation(DefaultTimeoutMs, $"OPC UA 采集 {name}", caller, LifetimeToken);
        DataValueCollection result = null!;
        Exception? failure = null;
        try
        {
            operation.ThrowIfActive();
            ExecutionCheck?.Invoke();
            lock (_state) CheckAvailable();
            result = AcquisitionReader == null
                ? (await Session.ReadAsync(null, 0, TimestampsToReturn.Both, nodes, operation.Token).ConfigureAwait(false)).Results
                : await AcquisitionReader(nodes, operation.Token).ConfigureAwait(false);
            operation.ThrowIfActive();
            ExecutionCheck?.Invoke();
            lock (_state) CheckAvailable();
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            try { await operation.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup)
            { failure = failure == null ? cleanup : new AggregateException("OPC UA 采集请求与取消回调均有失败", failure, cleanup); }
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }

    public void Isolate(string reason) { lock (_state) _isolated ??= reason; }

    public async Task CloseAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        ct.ThrowIfCancellationRequested();
        Task actual;
        lock (_state)
        {
            if (_closed) return;
            _closing = true;
            if (RetirementKey != null)
                RetiringResources.Track(RetirementKey, this, CloseAsync, () => IsClosed,
                    () => IsolatedReason ?? LastCloseError?.Message ?? "OPC UA Session 正在关闭");
            if (_closeTask == null || _closeTask.IsFaulted || _closeTask.IsCanceled)
            {
                _closeTask = Task.Run(CloseCoreAsync, CancellationToken.None);
                _ = _closeTask.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                if (RetirementKey != null)
                    _ = _closeTask.ContinueWith(_ => RetiringResources.ForgetReleased(RetirementKey, this), CancellationToken.None,
                        TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            actual = _closeTask;
        }
        try { await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); LastCloseError = null; }
        catch (TimeoutException ex) when (!actual.IsCompleted)
        {
            var timeout = new TimeoutException($"OPC UA 关闭超过 {timeoutMs} ms，真实请求/采集/订阅或 Session 释放尚未退出；保留引用及隔离，不能报告已断开", ex);
            LastCloseError = timeout; throw timeout;
        }
        catch (Exception ex) { LastCloseError = ex; throw; }
    }

    private async Task CloseCoreAsync()
    {
        _lifetime.Cancel();
        Task[] pending;
        OpcUaDataAcqTask[] collectors;
        lock (_state)
        {
            collectors = _collectors.ToArray();
            pending = _requests.Concat(_activities.Select(a => a.Task)).ToArray();
        }
        foreach (var task in collectors) task.Dispose();
        pending = pending.Concat(collectors.Select(task => task.CloseCompletion)).ToArray();
        foreach (var task in pending)
        {
            try { await task.ConfigureAwait(false); }
            catch (Exception ex) { Trace.TraceWarning($"OPC UA 收尾已观察到原始任务退出及其失败：{ex}"); }
        }
        await _close().ConfigureAwait(false);
        lock (_state) _closed = true;
        _lifetime.Dispose();
    }

    private void CheckAvailable()
    {
        if (_closing || _closed) throw new InvalidOperationException("OPC UA 连接正在关闭或已关闭，请由序列重新连接");
        if (_isolated != null) throw new InvalidOperationException($"OPC UA 连接已隔离：{_isolated}");
    }

    public void Dispose() => _ = ObserveCloseAsync();
    private async Task ObserveCloseAsync()
    {
        try { await CloseAsync(2000).ConfigureAwait(false); }
        catch (Exception ex)
        {
            Trace.TraceError($"OPC UA 连接收尾未确认，保留任务和 Session 引用：{ex}");
            try { _report?.Invoke(ex); }
            catch (Exception reportError) { Trace.TraceError($"OPC UA 收尾诊断回调失败：{reportError}"); }
        }
    }

    private sealed class Activity(OpcUaConnectionResource owner, TaskCompletionSource finished) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            finished.TrySetResult();
            lock (owner._state) owner._activities.Remove(finished);
        }
    }
}
