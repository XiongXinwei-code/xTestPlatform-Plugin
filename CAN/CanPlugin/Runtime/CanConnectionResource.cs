using System.Collections.Concurrent;
using System.Diagnostics;
using CAN.Adapters;
using CAN.CANopen;
using CAN.Models;
using PluginCommon;

namespace CAN.Runtime;

/// <summary>连接所有者：真实 I/O/回调/受控停机退出后释放句柄，另行确认普通托管任务退出。</summary>
public sealed class CanConnectionResource : ICanAdapter, ICanAdapterDiagnostics
{
    private static readonly ConcurrentDictionary<string, CanConnectionResource> PhysicalOwners = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<CanConnectionResource, Task> RetiredWorkers = new();
    private readonly object _gate = new();
    private readonly CanSubmissionGate _txGate = new();
    private readonly SemaphoreSlim _receiveModeGate = new(1, 1);
    private int _routingRequests;
    private TaskCompletionSource _receiveModeChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ICanAdapter _raw;
    private readonly string _physicalKey;
    private readonly CancellationTokenSource _receiveStop = new();
    private readonly CancellationTokenSource _foregroundStop = new();
    private readonly OwnerStopSignal _foregroundWait = new();
    private readonly Action<Exception>? _reportCloseError;
    private readonly CanReceiveRouter _router = new();
    private readonly Dictionary<string, CanopenCyclicService> _services = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CanBackgroundJob> _jobs = new(StringComparer.Ordinal);
    private readonly HashSet<uint> _claimedIds = [];
    private sealed record SenderClaim(HashSet<uint> All, HashSet<uint> Sync, HashSet<uint> Exclusive, bool Diagnostic);
    private readonly Dictionary<CanopenCyclicService, SenderClaim> _senders = [];
    private readonly Dictionary<CanopenCyclicService, bool> _startingSenders = [];
    private bool OwnsSender(CanopenCyclicService? service)
    { lock (_gate) return service != null && _senders.ContainsKey(service); }
    private readonly AsyncLocal<CanopenCyclicService?> _ownerCommand = new();
    private readonly AsyncLocal<CancellationToken> _ownerCommandToken = new();
    private readonly AsyncLocal<CanConfirmationBudget?> _ownerCommandBudget = new();
    private readonly AsyncLocal<bool> _ownerCommandConfirmed = new();
    private Task? _receiveTask, _closeTask;
    private Task _foregroundCancellation = Task.CompletedTask;
    private int _activeIo;
    private bool _controlled, _closing, _closed, _rawProtocolClaimed;
    private CanProtocolType? _openedProtocol;
    private string? _isolation;
    private string _closePhase = "运行中";
    private string? _closeError;
    private Exception? _nativeCancellationError;
    public Guid Epoch { get; } = Guid.NewGuid();
    public bool SupportsConfirmedTransmit => _raw is ICanConfirmedTransmitter { ConfirmationAvailable: true };
    internal void VerifySubmissionAvailable() => (_raw as ICanBudgetedBatchSubmitter)?.VerifySubmissionAvailable();
    internal void VerifyConfirmationAvailable(int timeoutMs, CancellationToken ct = default, bool requireOrderedBatch = false)
    {
        ct.ThrowIfCancellationRequested();
        long started = Stopwatch.GetTimestamp();
        int admitted = 0;
        var observationStop = ForegroundWaitSignal;
        using var stepOperation = observationStop == null ? null : ExecutionScopedExecutor.OwnOperation(() =>
        {
            if (Volatile.Read(ref admitted) != 0) Isolate("CAN 发送确认预检步骤取消或退出，原始预检尚待确认退出");
        });
        var task = Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException("发送能力预检在实际提交前已超时");
            lock (_gate) { CheckLocked(); _activeIo++; Volatile.Write(ref admitted, 1); }
            try
            {
                ct.ThrowIfCancellationRequested(); if (observationStop != null) ExecutionScopedExecutor.ThrowIfCurrentStepRetired();
                if (requireOrderedBatch && _raw is ICanBudgetedConfirmedBatchTransmitter batch) batch.VerifyOrderedBatchAvailable();
                if (_raw is ICanBudgetedConfirmationProbe configured)
                    configured.VerifyConfirmationAvailable(new(started, timeoutMs, ct), ct);
                else (_raw as ICanConfirmedTransmitter)?.VerifyConfirmationAvailable();
            }
            catch (Exception ex)
            {
                if (_raw is ICanBudgetedConfirmationProbe { HasUnresolvedTransmit: true })
                    Isolate("确认预检未排空旧发送，禁止新提交但允许独立 CAN_Close；" + ex.Message);
                throw;
            }
            finally { lock (_gate) _activeIo--; }
        });
        try
        {
            if (observationStop == null) task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).GetAwaiter().GetResult();
            else observationStop.WaitAsync(task, TimeSpan.FromMilliseconds(timeoutMs), ct, "CAN 发送能力预检").GetAwaiter().GetResult();
        }
        catch
        {
            if (!task.IsCompleted) Isolate("发送能力预检尚未实际退出");
            _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            throw;
        }
    }
    internal Task<bool> ConfirmTransmitQuiescentAsync() => Task.Run(() =>
    {
        void Check() { lock (_gate) CheckLocked(); }
        using var turn = _txGate.Enter(null, default, Check, uint.MaxValue);
        lock (_gate) { CheckLocked(); _activeIo++; }
        try { return _raw is ICanTransmitQuiescence capability && capability.ConfirmTransmitQuiescent(); }
        finally { lock (_gate) _activeIo--; }
    });
    public bool IsConnected { get { lock (_gate) return !_closed && _raw.IsConnected &&
        (!_closing || OwnsSender(_ownerCommand.Value)); } }
    public bool Controlled { get { lock (_gate) return _controlled; } }
    public bool IsClosed { get { lock (_gate) return _closed; } }
    internal bool HasPendingManagedWorkers { get { lock (_gate) return _jobs.Values.Any(j => !j.Completion.IsCompleted); } }
    internal bool IsClosing { get { lock (_gate) return _closing; } }
    // Owned stop commands may still complete through this connection after ordinary
    // foreground observers retire. They retain the service's separate stop budget.
    internal OwnerStopSignal? ForegroundWaitSignal => _ownerCommand.Value != null &&
        OwnsSender(_ownerCommand.Value) ? null : _foregroundWait;
    public string? IsolationReason { get { lock (_gate) return _isolation; } }

    /// <summary>Only wait/retry an owner already closing; an active connection is never taken over.</summary>
    public static async Task WaitForPreviousOwnerAsync(string physicalKey, int timeoutMs, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        var watch = Stopwatch.StartNew();
        while (PhysicalOwners.TryGetValue(physicalKey, out var previous))
        {
            ct.ThrowIfCancellationRequested();
            if (previous.IsClosed) { previous.ReleasePhysicalClaim(); continue; }
            if (!previous.IsClosing)
                throw new InvalidOperationException($"物理 CAN 通道 {physicalKey} 仍由活动连接使用，不能接管。{previous.GetLifecycleDiagnostics()}");
            int remaining = timeoutMs - (int)Math.Min(int.MaxValue, watch.ElapsedMilliseconds);
            if (remaining <= 0) throw new TimeoutException($"等待物理 CAN 通道 {physicalKey} 旧连接退出超过 {timeoutMs} ms。{previous.GetLifecycleDiagnostics()}");
            try { await previous.ClosePhysicalAsync(remaining, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) when (previous.IsClosed)
            { previous.ReleasePhysicalClaim(); /* 历史错误仍在旧 owner 诊断；真实释放允许首次重跑。 */ }
            catch (TimeoutException ex)
            { throw new TimeoutException($"物理 CAN 通道 {physicalKey} 旧连接尚未完成实际退出；不能重开。{previous.GetLifecycleDiagnostics()}", ex); }
            catch (Exception ex)
            { throw new InvalidOperationException($"物理 CAN 通道 {physicalKey} 旧连接收尾失败；保留未释放句柄供下一次关闭重试。{previous.GetLifecycleDiagnostics()}", ex); }
        }
    }

    public CanConnectionResource(ICanAdapter raw, string? physicalKey = null, Action<Exception>? reportCloseError = null)
    {
        _raw = raw ?? throw new ArgumentNullException(nameof(raw));
        _reportCloseError = reportCloseError;
        _physicalKey = physicalKey ?? Guid.NewGuid().ToString();
        if (!PhysicalOwners.TryAdd(_physicalKey, this))
            throw new InvalidOperationException($"物理 CAN 通道 {_physicalKey} 已打开或仍在退出/隔离；请先等待旧连接实际关闭，不能重复打开");
    }

    public void Open(CanAdapterConfig config) => OpenCore(config, null);

    private void OpenCore(CanAdapterConfig config, Action? checkSubmission)
    {
        lock (_gate)
        {
            checkSubmission?.Invoke();
            if (_closing || _closed) throw new InvalidOperationException("CAN 连接已进入收尾，不能再打开");
            _openedProtocol = config.Protocol;
            _activeIo++;
        }
        try
        {
            try { checkSubmission?.Invoke(); _raw.Open(config); }
            catch (Exception openError)
            {
                bool released = false;
                try { _raw.Dispose(); released = true; }
                catch (Exception cleanupError)
                {
                    released = _raw is ICanCloseState { HandlesReleased: true };
                    lock (_gate) _closeError = $"{cleanupError.GetType().Name}: {cleanupError.Message}";
                    throw new AggregateException(released ? "CAN 打开失败，句柄已释放但清理报告错误" : "CAN 打开失败且清理未确认，请执行 CAN_Close 重试", openError, cleanupError);
                }
                finally
                {
                    lock (_gate)
                    {
                        _closing = true; _closed = released;
                        _closePhase = released ? "打开失败，句柄已释放" : "打开失败，等待清理重试";
                        if (!released) _isolation = "打开失败，驱动句柄释放未确认";
                    }
                    if (released) ReleasePhysicalClaim();
                }
                throw;
            }
            lock (_gate)
            {
                if (_closing) throw new OperationCanceledException("CAN 打开完成前连接已请求关闭，拒绝迟到发布");
            }
        }
        finally { lock (_gate) _activeIo--; }
    }

    public Task OpenAsync(CanAdapterConfig config, int timeoutMs, CancellationToken ct = default)
        => OpenAsync(config, timeoutMs, ct, null);

    internal async Task OpenAsync(CanAdapterConfig config, int timeoutMs, CancellationToken ct, Action? checkLifetime)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs); ct.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        void CheckBudget()
        {
            ExecutionScopedExecutor.ThrowIfCurrentStepRetired();
            checkLifetime?.Invoke();
            ct.ThrowIfCancellationRequested();
            if (clock.ElapsedMilliseconds >= timeoutMs) throw new TimeoutException($"CAN 打开总期限 {timeoutMs} ms 已到");
        }
        var actual = Task.Run(() => OpenCore(config, CheckBudget));
        try
        {
            await _foregroundWait.WaitAsync(actual, TimeSpan.FromMilliseconds(timeoutMs), ct, "CAN 打开").ConfigureAwait(false);
            CheckBudget();
        }
        catch
        {
            Dispose();
            _ = actual.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            throw;
        }
    }

    /// <summary>原始协议独占连接的接收模式；不能在同一连接后续切换受控 CANopen。</summary>
    public void ClaimRawProtocol()
    {
        lock (_gate)
        {
            CheckLocked();
            if (_controlled) throw new InvalidOperationException("受控 CANopen 连接禁止混用 UDS/XCP 原始读取，请使用独立连接");
            _rawProtocolClaimed = true;
        }
    }

    private void CheckLocked(bool finishingOwnedBatch = false)
    {
        if (_closed || (_closing && !finishingOwnedBatch && !OwnsSender(_ownerCommand.Value)))
            throw new InvalidOperationException("CAN 连接正在关闭或已关闭");
        if (_isolation != null) throw new InvalidOperationException($"CAN 连接已隔离：{_isolation}");
        if (!_raw.IsConnected) throw new InvalidOperationException("CAN 连接未打开");
    }

    public CanopenCyclicService StartService(string name, FeedbackRoute[] routes)
        => StartService(name, routes, 1000);
    internal Task<CanopenCyclicService> StartServiceAsync(string name, FeedbackRoute[] routes, int timeoutMs,
        CancellationToken ct, Action checkAdmission) => Task.Run(() =>
        { checkAdmission(); return StartService(name, routes, timeoutMs, ct); }, CancellationToken.None);

    public CanopenCyclicService StartService(string name, FeedbackRoute[] routes, int timeoutMs, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("服务名称不能为空");
        EnsureSharedReceive(timeoutMs, ct);
        lock (_gate)
        {
            CheckLocked();
            if (_services.ContainsKey(name)) throw new InvalidOperationException($"CANopen 服务 {name} 已存在，请先确认停止");
            var subscription = _router.Subscribe(routes, allowEmpty: true);
            var service = new CanopenCyclicService(this, subscription);
            _services.Add(name, service);
            return service;
        }
    }

    internal void EnsureSharedReceive(int timeoutMs, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs); ct.ThrowIfCancellationRequested();
        lock (_gate) { CheckLocked(); if (_controlled) return; _routingRequests++; }
        long started = Stopwatch.GetTimestamp();
        // Never hold the connection lock while the previous native reader exits.
        bool entered = false;
        try
        {
            if (!_receiveModeGate.Wait(timeoutMs, ct)) throw new TimeoutException("等待原始CAN读取实际退出超时，未启动第二个接收者");
            entered = true;
            lock (_gate) { CheckLocked(); if (_controlled) return; }
            int remaining = timeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (remaining <= 0) throw new TimeoutException("共享CAN接收转换总期限已到");
            using var transition = CAN.CANopen.CanopenConnectionAccess.For(this).EnterControlledMode(_router.Publish, remaining, ct);
            lock (_gate)
            {
                CheckLocked(); ct.ThrowIfCancellationRequested(); if (_controlled) return;
                if (_rawProtocolClaimed) throw new InvalidOperationException("该连接已供UDS/XCP原始协议使用，本次仅开放CAN/CANopen/CiA402混用");
                if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                    throw new TimeoutException("共享CAN接收转换总期限已到，未启动第二个接收者");
                _controlled = true; _receiveTask = Task.Run(ReceiveLoop);
            }
        }
        finally
        {
            lock (_gate)
            {
                _routingRequests--;
                var previous = _receiveModeChanged; _receiveModeChanged = new(TaskCreationOptions.RunContinuationsAsynchronously); previous.TrySetResult();
            }
            if (entered) _receiveModeGate.Release();
        }
    }
    internal CanFrameInbox CreateNodeInbox()
    { lock (_gate) { CheckLocked(); return _router.CreateNodeInbox(); } }

    public CanopenCyclicService GetService(string name)
    {
        lock (_gate) return _services.TryGetValue(name, out var service) ? service :
            throw new InvalidOperationException($"CANopen 服务 {name} 不存在");
    }

    public Task StopServiceAsync(string name, int timeoutMs, CancellationToken ct)
        => StopServiceAsync(name, GetService(name), timeoutMs, ct);

    internal async Task StopServiceAsync(string name, CanopenCyclicService service, int timeoutMs, CancellationToken ct)
    {
        // Close the captured generation only. A replacement with the same name is
        // unrelated, including when this call cleans up a failed old start.
        try { await service.CloseAsync(timeoutMs, ct).ConfigureAwait(false); }
        finally
        {
            // Never take the service gate while holding the connection gate: service
            // stop confirmation takes them in the opposite order to release its sender.
            bool closed = service.IsClosed;
            lock (_gate) if (closed && _services.TryGetValue(name, out var current) && ReferenceEquals(current, service)) _services.Remove(name);
        }
    }

    private void ReceiveLoop()
    {
        Exception? failure = null;
        try
        {
            while (!_receiveStop.IsCancellationRequested)
            {
                var frame = _raw.Read(50, _receiveStop.Token);
                if (frame != null) _router.Publish(frame);
            }
        }
        catch (OperationCanceledException) when (_receiveStop.IsCancellationRequested) { }
        catch (Exception ex) { failure = ex; Isolate($"后台接收失败：{ex.Message}"); }
        finally { _router.Finish(failure); }
    }

    private async Task<CanMessage?> ReadSharedAsync(Func<Task<CanMessage?>> read, int timeoutMs, CancellationToken ct)
    {
        lock (_gate) CheckLocked(); ct.ThrowIfCancellationRequested(); long started = Stopwatch.GetTimestamp();
        var frame = await read().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested(); lock (_gate) CheckLocked();
        if (frame != null && timeoutMs > 0 && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
            throw new TimeoutException($"共享CAN接收结果超过配置期限{timeoutMs}ms，未发布迟到结果");
        return frame;
    }
    public Task<CanMessage?> ReadCanopenAsync(uint id, int timeoutMs, CancellationToken ct)
        => ReadSharedAsync(() => _router.ReadAsync(id, timeoutMs, ct, ForegroundWaitSignal), timeoutMs, ct);
    internal Task<CanMessage?> ReadNodeInboxAsync(CanFrameInbox inbox, uint id, int timeoutMs, CancellationToken ct)
        => ReadSharedAsync(() => inbox.ReadAsync(id, timeoutMs, ct, ForegroundWaitSignal), timeoutMs, ct);
    private Task<CanMessage?> ReadSharedRawAsync(uint? id, int timeoutMs, CancellationToken ct)
        => ReadSharedAsync(() => _router.ReadRawAsync(id, timeoutMs, ct, ForegroundWaitSignal), timeoutMs, ct);

    internal void BeginSdoTransaction(byte node)
    {
        if (Controlled) _router.Discard(0x580u + node);
    }

    public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => ReadRaw(null, timeoutMs, ct);
    public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => ReadRaw(id, timeoutMs, ct);
    public async Task<CanMessage?> ReadAsync(uint? id, int timeoutMs, CancellationToken ct = default)
    {
        if (timeoutMs == 0 || timeoutMs < Timeout.Infinite) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        ct.ThrowIfCancellationRequested();
        if (Controlled) return await ReadSharedRawAsync(id, timeoutMs, ct).ConfigureAwait(false);
        long started = Stopwatch.GetTimestamp();
        int admitted = 0;
        using var stepOperation = ForegroundWaitSignal == null ? null : ExecutionScopedExecutor.OwnOperation(() =>
        {
            if (Volatile.Read(ref admitted) != 0) Isolate("原始 CAN 接收步骤取消或退出，事务结果未确认消费");
        });
        var actual = Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            int remaining = timeoutMs == Timeout.Infinite ? Timeout.Infinite :
                timeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (timeoutMs != Timeout.Infinite && remaining <= 0) throw new TimeoutException("CAN 接收在提交前已超时");
            return ReadRaw(id, remaining, ct, () => Volatile.Write(ref admitted, 1));
        }, CancellationToken.None);
        try
        {
            var stop = ForegroundWaitSignal;
            var result = stop == null
                ? await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false)
                : await stop.WaitAsync(actual, TimeSpan.FromMilliseconds(timeoutMs), ct, "CAN 接收").ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (timeoutMs != Timeout.Infinite && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"CAN 接收结果返回时已超过配置期限 {timeoutMs} ms，不能发布迟到结果");
            return result;
        }
        catch
        {
            _ = actual.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            if (!actual.IsCompleted && Volatile.Read(ref admitted) != 0) Isolate("原始接收已停止等待但实际调用尚未退出");
            throw;
        }
    }
    private CanMessage? ReadRaw(uint? id, int timeoutMs, CancellationToken ct, Action? admitted = null)
    {
        long started = Stopwatch.GetTimestamp();
        int Remaining() => timeoutMs == Timeout.Infinite ? Timeout.Infinite : Math.Max(0, timeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
        while (true)
        {
            Task? transition; bool shared;
            lock (_gate) { CheckLocked(); shared = _controlled; transition = _routingRequests > 0 ? _receiveModeChanged.Task : null; }
            if (timeoutMs > 0 && Remaining() == 0) return null;
            if (shared) return ReadSharedRawAsync(id, Remaining(), ct).GetAwaiter().GetResult();
            if (transition != null)
            {
                try
                {
                    if (ForegroundWaitSignal is { } stop) stop.WaitAsync(transition, TimeSpan.FromMilliseconds(Remaining()), ct, "等待共享接收转换").GetAwaiter().GetResult();
                    else transition.WaitAsync(TimeSpan.FromMilliseconds(Remaining()), ct).GetAwaiter().GetResult();
                }
                catch (TimeoutException) { return null; }
                continue;
            }
            if (!_receiveModeGate.Wait(Remaining(), ct)) return null;
            try
            {
                lock (_gate) { CheckLocked(); if (_controlled || _routingRequests != 0) continue; }
                if (timeoutMs > 0 && Remaining() == 0) return null;
                return ReadNative(id, Remaining(), ct, admitted);
            }
            finally { _receiveModeGate.Release(); }
        }
    }
    private CanMessage? ReadNative(uint? id, int timeoutMs, CancellationToken ct, Action? admitted)
    {
        if (_ownerCommand.Value == null) CanExecutionGuard.CheckSubmission();
        lock (_gate)
        {
            CheckLocked();
            _activeIo++;
            admitted?.Invoke();
        }
        try
        {
            // Do not stop the controlled RX/echo pump here; only cancel foreground raw readers.
            // Owner-controlled shutdown commands keep their own cancellation budget.
            var cancellation = new CanIoCancellation(ct,
                _ownerCommand.Value == null ? _foregroundStop.Token : CancellationToken.None, Timeout.Infinite);
            CanMessage? result = null;
            Exception? failure = null;
            try
            {
                if (_ownerCommand.Value == null) CanExecutionGuard.CheckSubmission();
                if (_ownerCommand.Value == null) ExecutionScopedExecutor.ThrowIfCurrentStepRetired();
                ct.ThrowIfCancellationRequested(); cancellation.Token.ThrowIfCancellationRequested();
                result = id.HasValue ? _raw.Read(id.Value, timeoutMs, cancellation.Token) : _raw.Read(timeoutMs, cancellation.Token);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try { cancellation.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception cleanup)
                {
                    failure = failure == null ? cleanup : new AggregateException("CAN 原始接收与取消回调均失败", failure, cleanup);
                    Interlocked.CompareExchange(ref _nativeCancellationError, failure, null);
                }
            }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }
        finally { lock (_gate) _activeIo--; }
    }

    /// <summary>
    /// Bounded foreground submission. Completed means the driver call returned, not on-bus ACK.
    /// A timed-out native call keeps Write's active-I/O ownership until its actual exit.
    /// </summary>
    public async Task WriteAsync(CanMessage message, int timeoutMs, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        var copy = CanReceiveRouter.Copy(message);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        int admitted = 0;
        using var stepOperation = ForegroundWaitSignal == null ? null : ExecutionScopedExecutor.OwnOperation(() =>
        {
            if (Volatile.Read(ref admitted) != 0) Isolate("原始 CAN 发送步骤取消或退出，实际发送或结果尚待确认");
        });
        var actual = Task.Run(() => WriteCore(copy, ct, started, timeoutMs, admitted: () => Volatile.Write(ref admitted, 1)), CancellationToken.None);
        try
        {
            var stop = ForegroundWaitSignal;
            if (stop == null) await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
            else await stop.WaitAsync(actual, TimeSpan.FromMilliseconds(timeoutMs), ct, "CAN 发送").ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"CAN 发送结果返回时已超过配置期限 {timeoutMs} ms；原始提交可能已经完成，不能视为撤回");
        }
        catch (Exception ex)
        {
            _ = actual.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            if (!actual.IsCompleted && Volatile.Read(ref admitted) != 0)
                Isolate($"原始发送停止等待但实际 I/O 尚未退出，配置期限={timeoutMs} ms；不能假设报文已撤回");
            if (ex is OperationCanceledException && ct.IsCancellationRequested) throw;
            if (ex is TimeoutException)
                throw new TimeoutException($"CAN 发送等待超时或驱动报告超时，配置期限={timeoutMs} ms；实际 I/O 已退出={actual.IsCompleted}，不代表撤回发送", ex);
            if (ex is OperationCanceledException)
                throw new InvalidOperationException("CAN 发送被底层取消，但并非用户主动取消", ex);
            throw;
        }
    }

    public void Write(CanMessage message)
    {
        var copy = CanReceiveRouter.Copy(message);
        WriteCore(copy, CancellationToken.None, 0, 0);
    }

    /// <summary>持久任务使用其自身取消及执行期身份，不继承启动步骤的短令牌。</summary>
    internal void Write(CanMessage message, CancellationToken lifetimeToken, Action checkLifetime)
    {
        var copy = CanReceiveRouter.Copy(message);
        WriteCore(copy, lifetimeToken, 0, 0, checkLifetime);
    }

    private void WriteCore(CanMessage copy, CancellationToken callerToken, long started, int timeoutMs, Action? checkLifetime = null, Action? admitted = null)
    {
        CheckSubmission();
        CAN.Helpers.CanHelper.ValidateTransmitFrame(copy);
        // Reject an already isolated/closing owner without waiting for a native
        // submission that may still hold the transmit gate. Recheck after the
        // gate is acquired to cover a concurrent close/isolation transition.
        lock (_gate) CheckLocked();
        var budget = timeoutMs > 0 ? new CanConfirmationBudget(started, timeoutMs, callerToken) : _ownerCommandBudget.Value;
        using var protocolTurn = CanopenConnectionAccess.For(this).EnterSupplementalSubmission(
            copy.FrameType == CanFrameType.Standard && !copy.IsFd && copy.Id is >= 0x601 and <= 0x67F, budget, callerToken, () =>
            { CheckSubmission(); lock (_gate) CheckLocked(); });
        using (_txGate.Enter(budget,
            callerToken, CheckSubmission, copy.Id))
        {
            lock (_gate)
            {
                // Check the original token and monotonic deadline, not cancellation
                // callbacks or the outer waiter's continuation, before admitting I/O.
                CheckSubmission();
                CheckLocked();
                if (copy.IsFd && _openedProtocol == CanProtocolType.Classic)
                    throw new ArgumentException("连接以 Classic CAN 打开，不能发送 CAN FD 帧；请使用 FD 配置重新打开，不能自动降级或截断数据");
                _activeIo++;
                admitted?.Invoke();
            }
            try
            {
                CheckSubmission();
                using var nativeWrite = CanBackgroundJob.Current?.EnterNativeWrite();
                if (_ownerCommand.Value != null && _ownerCommandConfirmed.Value && _raw is ICanConfirmedTransmitter confirmed)
                    WriteConfirmed(confirmed, copy, _ownerCommandBudget.Value
                        ?? throw new InvalidOperationException("受控所有者发送缺少配置预算"), _ownerCommandToken.Value);
                else if (_ownerCommand.Value != null && !_ownerCommandConfirmed.Value && _raw is ICanBudgetedBatchSubmitter submitter)
                    submitter.WriteBatchSubmitted([copy], _ownerCommandBudget.Value
                        ?? throw new InvalidOperationException("受控所有者提交缺少配置预算"), _ownerCommandToken.Value,
                        CheckSubmission, _ => { }, _ => { });
                else _raw.Write(copy);
            }
            finally { lock (_gate) _activeIo--; }
        }

        void CheckSubmission()
        {
            checkLifetime?.Invoke();
            if (_ownerCommand.Value == null) { CanExecutionGuard.CheckSubmission(); ExecutionScopedExecutor.ThrowIfCurrentStepRetired(); }
            else if (_ownerCommandBudget.Value is { } ownerBudget) _ = ownerBudget.Remaining(_ownerCommandToken.Value);
            callerToken.ThrowIfCancellationRequested();
            if (timeoutMs > 0 && System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"CAN 发送排队已超过配置期限 {timeoutMs} ms，未提交此帧");
        }
    }

    internal IDisposable EnterSenderStart(CanopenCyclicService service, bool diagnostic)
    {
        lock (_gate)
        {
            CheckLocked();
            if (_startingSenders.ContainsKey(service)) throw new InvalidOperationException("该发送源正在启动，未执行驱动预检");
            if (_senders.TryGetValue(service, out var own) && own.Diagnostic != diagnostic)
                throw new ArgumentException("同一发送源不能混用诊断确认和周期仅提交，未执行驱动预检");
            if (_senders.Any(pair => !ReferenceEquals(pair.Key, service) && (diagnostic || pair.Value.Diagnostic)) ||
                _startingSenders.Any(pair => !ReferenceEquals(pair.Key, service) && (diagnostic || pair.Value)))
                throw new InvalidOperationException("逐批确认诊断必须独占发送源，未执行驱动预检");
            if (diagnostic && _jobs.Count != 0) throw new InvalidOperationException("请先停止通用周期任务后再启动独占诊断");
            _startingSenders.Add(service, diagnostic);
            return new SenderStartLease(this, service);
        }
    }
    private sealed class SenderStartLease(CanConnectionResource connection, CanopenCyclicService service) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (connection._gate) connection._startingSenders.Remove(service);
        }
    }

    internal void ReserveSender(CanopenCyclicService service, uint[] ids, uint[] syncIds, bool diagnostic)
    {
        lock (_gate)
        {
            CheckLocked();
            if (_senders.ContainsKey(service)) throw new InvalidOperationException("该发送源已启动或尚未退出");
            if (diagnostic && _jobs.Count != 0) throw new InvalidOperationException("请先停止通用周期任务后再启动独占诊断");
            var claim = new SenderClaim(ids.ToHashSet(), syncIds.ToHashSet(), ids.Except(syncIds).ToHashSet(), diagnostic);
            CheckSenderClaim(service, claim); _senders.Add(service, claim);
            _claimedIds.UnionWith(ids);
        }
    }
    internal void ReleaseSender(CanopenCyclicService service)
    {
        lock (_gate) if (_senders.Remove(service))
        { _claimedIds.Clear(); foreach (var claim in _senders.Values) _claimedIds.UnionWith(claim.All); }
    }

    internal void UpdateSender(CanopenCyclicService service, uint[] ids, uint[] syncIds, bool diagnostic)
    {
        lock (_gate)
        {
            CheckLocked();
            if (!_senders.TryGetValue(service, out var previous)) throw new InvalidOperationException("发送源所有者已失效");
            var claim = new SenderClaim(previous.All.Union(ids).ToHashSet(), previous.Sync.Union(syncIds).ToHashSet(),
                previous.Exclusive.Union(ids.Except(syncIds)).ToHashSet(), diagnostic);
            CheckSenderClaim(service, claim); _senders[service] = claim;
            // Retain previous control IDs until this service really closes.
            // A later motion configuration must not expose an old target/SYNC
            // ID to raw senders while the same owner still controls the bus.
            _claimedIds.UnionWith(ids);
        }
    }

    private void CheckSenderClaim(CanopenCyclicService service, SenderClaim claim)
    {
        if (_startingSenders.Any(pair => !ReferenceEquals(pair.Key, service) && (claim.Diagnostic || pair.Value)))
            throw new InvalidOperationException("其它发送源正在进行独占诊断预检，未发布本次发送源");
        foreach (var pair in _senders)
        {
            if (ReferenceEquals(pair.Key, service)) continue;
            if (claim.Diagnostic || pair.Value.Diagnostic)
                throw new InvalidOperationException("逐批确认诊断必须独占发送源，不能阻塞其它周期提交");
            if (claim.All.Intersect(pair.Value.All).Any(id => claim.Exclusive.Contains(id) || pair.Value.Exclusive.Contains(id)))
                throw new InvalidOperationException("RPDO/历史独占ID与其它发送源冲突；只有SYNC允许多源共享");
        }
    }

    internal async Task RunOwnerCommandAsync(CanopenCyclicService owner, Func<Task> command, CanConfirmationBudget budget, CancellationToken ct,
        bool requireOnBusConfirmation = true)
    {
        lock (_gate) if (!OwnsSender(owner)) throw new InvalidOperationException("控制所有者已失效");
        // Do not hold _txGate across SDO: its Write runs on another task and requires that gate.
        var previous = _ownerCommand.Value; var previousToken = _ownerCommandToken.Value;
        var previousBudget = _ownerCommandBudget.Value;
        bool previousConfirmed = _ownerCommandConfirmed.Value;
        _ownerCommand.Value = owner; _ownerCommandToken.Value = ct;
        _ownerCommandBudget.Value = budget;
        _ownerCommandConfirmed.Value = requireOnBusConfirmation;
        try { await command().ConfigureAwait(false); }
        finally { _ownerCommand.Value = previous; _ownerCommandToken.Value = previousToken; _ownerCommandBudget.Value = previousBudget; _ownerCommandConfirmed.Value = previousConfirmed; }
    }

    internal void SendBatch(CanopenCyclicService owner, OrderedFrame[] frames, bool confirmed, CancellationToken ct,
        Action<CanBatchProgress> submitted, CanConfirmationBudget budget, Action<CanBatchDiagnostic>? publish = null,
        CanBatchPurpose purpose = CanBatchPurpose.Monitoring, CancellationToken queueWithdrawal = default,
        Func<OrderedFrame[]>? prepareAtAdmission = null)
    {
        frames = frames.Select(f => f with { Data = f.Data.ToArray() }).ToArray();
        owner.ValidateSendBatch(frames, purpose);
        // Validate the whole immutable batch before any member can enter the driver.
        foreach (var frame in frames)
            CAN.Helpers.CanHelper.ValidateTransmitFrame(new CanMessage { Id = frame.CobId, Data = frame.Data });
        long queued = Stopwatch.GetTimestamp();
        using var queueWait = CancellationTokenSource.CreateLinkedTokenSource(ct, queueWithdrawal);
        using var protocolTurn = CanopenConnectionAccess.For(this).EnterSupplementalSubmission(
            frames.Any(frame => frame.CobId is >= 0x601 and <= 0x67F), budget, queueWait.Token, () =>
            { owner.CheckSending(); lock (_gate) CheckLocked(finishingOwnedBatch: OwnsSender(owner)); });
        // Only a waiting whole batch can be withdrawn. After admission this
        // token is deliberately absent from the SDK/member submission path.
        using var turn = _txGate.Enter(budget, queueWait.Token, owner.CheckSending, frames[0].CobId);
        if (prepareAtAdmission != null)
        {
            frames = prepareAtAdmission().Select(frame => frame with { Data = frame.Data.ToArray() }).ToArray();
            owner.ValidateSendBatch(frames, purpose);
            foreach (var frame in frames)
                CAN.Helpers.CanHelper.ValidateTransmitFrame(new CanMessage { Id = frame.CobId, Data = frame.Data });
        }
        double lockMs = Stopwatch.GetElapsedTime(queued).TotalMilliseconds;
        long generation = owner.DiagnosticGeneration;
        long motionGeneration = owner.DiagnosticMotionGeneration;
        int batchAdmitted = 0, batchSettled = 0, unresolvedPrior = 0;
        bool unresolvedPriorQueue = false;
        void Record(CanBatchProgress value)
        {
            if (value.Kind == CanBatchProgressKind.NativeAdmission) batchAdmitted += value.Count;
            else if (value.Kind == CanBatchProgressKind.NativeNotEntered) batchAdmitted -= value.Count;
            else batchSettled += value.Count;
            submitted(value);
        }
        try
        {
            if (!confirmed && _raw is ICanBudgetedBatchSubmitter submitter)
            {
                void CheckSubmission()
                {
                    owner.CheckSending(); _ = budget.Remaining(ct);
                    lock (_gate)
                    {
                        CheckLocked(finishingOwnedBatch: OwnsSender(owner));
                        if (!OwnsSender(owner)) throw new InvalidOperationException("批次提交所有权已失效");
                    }
                }
                CheckSubmission(); lock (_gate) _activeIo++;
                try
                {
                    submitter.WriteBatchSubmitted(frames.Select(frame => new CanMessage { Id = frame.CobId, Data = frame.Data }).ToArray(),
                        budget, ct, CheckSubmission, Record, value => publish?.Invoke(value with
                        { ConnectionLockWaitMs = lockMs, ConnectionEpoch = Epoch, ServiceGeneration = generation,
                            MotionGeneration = motionGeneration, Purpose = purpose.ToString() }));
                }
                finally { lock (_gate) _activeIo--; }
                return;
            }
            if (confirmed && _raw is ICanBudgetedConfirmedBatchTransmitter batch && (frames.Length > 1 || batch.OrderedBatchAvailable))
            {
                void CheckAdmission()
                {
                    owner.CheckSending(); _ = budget.Remaining(ct);
                    lock (_gate)
                    {
                        CheckLocked(finishingOwnedBatch: OwnsSender(owner));
                        if (!OwnsSender(owner)) throw new InvalidOperationException("批次发送所有权已失效");
                    }
                }
                CheckAdmission(); lock (_gate) _activeIo++;
                try
                {
                    batch.WriteBatchConfirmed(frames.Select(f => new CanMessage { Id = f.CobId, Data = f.Data }).ToArray(), budget,
                        ct, CheckAdmission, Record, value =>
                        {
                            unresolvedPrior = value.UnresolvedPriorFrames;
                            unresolvedPriorQueue = value.UnresolvedPriorQueue;
                            publish?.Invoke(value with
                            { ConnectionLockWaitMs = lockMs, ConnectionEpoch = Epoch, ServiceGeneration = generation, MotionGeneration = motionGeneration,
                                Purpose = purpose.ToString() });
                        });
                }
                finally { lock (_gate) _activeIo--; }
                return;
            }
            int attempted = 0, completed = 0; Exception? failure = null;
            var submissions = new List<CanAdapterSubmissionEvidence>();
            var diagnostic = new CanBatchDiagnostic(Guid.NewGuid(), confirmed ? "SequentialConfirmedCompatibility" : "UnconfirmedSubmission", frames.Length, "Sending")
            { ConnectionLockWaitMs = lockMs, ConnectionEpoch = Epoch, ServiceGeneration = generation, MotionGeneration = motionGeneration,
                Purpose = purpose.ToString() };
            try
            {
            foreach (var frame in frames)
            {
                owner.CheckSending(); // 不依赖异步取消回调何时传递到 linked token。
                _ = budget.Remaining(ct);
                lock (_gate)
                {
                    CheckLocked(finishingOwnedBatch: OwnsSender(owner));
                    if (!OwnsSender(owner)) throw new InvalidOperationException("发送所有权已失效");
                    _activeIo++;
                }
                try
                {
                    var message = new CanMessage { Id = frame.CobId, Data = frame.Data.ToArray() };
                    Record(new(CanBatchProgressKind.NativeAdmission, 1)); attempted++; // 可能提交；兼容驱动不伪造精确接受范围。
                    int index = submissions.Count;
                    var evidence = new CanAdapterSubmissionEvidence(index, message.Id, message.FrameType.ToString(), message.IsFd,
                        message.Dlc, Convert.ToHexString(message.Data), confirmed ? "WriteConfirmed" : "Write",
                        Stopwatch.GetTimestamp(), null, null, false, null);
                    submissions.Add(evidence);
                    bool returned = false, adapterEntered = false; Exception? callError = null;
                    try
                    {
                        if (confirmed) WriteConfirmed((ICanConfirmedTransmitter)_raw, message, budget, ct, () => adapterEntered = true);
                        else { adapterEntered = true; _raw.Write(message); }
                        returned = true;
                        if (confirmed) { Record(new(CanBatchProgressKind.FrameConfirmed, 1)); completed++; }
                        else Record(new(CanBatchProgressKind.SubmissionCompleted, 1));
                    }
                    catch (Exception ex)
                    {
                        callError = ex;
                        if (!adapterEntered)
                        {
                            attempted--; submissions.RemoveAt(index);
                            try { Record(new(CanBatchProgressKind.NativeNotEntered, 1)); }
                            catch (Exception rollback) { throw new AggregateException("提交前取消及未进入适配器的计数回收均失败", ex, rollback); }
                        }
                        throw;
                    }
                    finally { if (adapterEntered) submissions[index] = evidence with { ReturnedMonotonicTicks = Stopwatch.GetTimestamp(), CallSucceeded = returned,
                        ConfirmationReported = returned && confirmed && callError == null, Error = callError?.ToString() }; }
                }
                finally { lock (_gate) _activeIo--; }
            }
            }
            catch (Exception ex) { failure = ex; throw; }
            finally
            {
                try { publish?.Invoke(diagnostic with { Phase = failure == null ? "Completed" : "Failed", AttemptedFrames = attempted,
                    ConfirmedFrames = completed, ReportedConfirmedFrames = completed, KnownAcceptedFrames = null,
                    SubmissionState = attempted == 0 ? "NotAttempted" : failure == null ? "AdapterCallsReturned" : "AdapterCallFailedOrUnknown",
                    AdapterCallExited = submissions.Count == 0 ? null : submissions.All(item => item.ReturnedMonotonicTicks != null),
                    NativeEvidenceUnavailableReason = "ICanAdapter.Write/旧确认接口不暴露原生调用次数、退出与SDK接受范围；Submissions仅记录主机适配器调用事实",
                    Submissions = submissions.ToArray(), Error = failure?.ToString() }); }
                catch (Exception diagnosticError) when (failure != null)
                { throw new AggregateException("适配器批次失败与诊断回调失败", failure, diagnosticError); }
            }
        }
        catch (Exception failure)
        {
            // Classify before releasing the connection gate: a queued SDO must
            // never overtake an unresolved/partial batch or old SDO drain.
            if (unresolvedPriorQueue || unresolvedPrior != 0 || (batchAdmitted > 0 && (batchSettled != batchAdmitted || batchAdmitted != frames.Length)))
            {
                owner.RecordSendFailure(failure);
                Isolate("发送链已返回但存在未确认发送、部分周期或旧 SDO 未决；禁止后续提交，允许独立 CAN_Close");
            }
            throw;
        }
    }

    private static void WriteConfirmed(ICanConfirmedTransmitter transmitter, CanMessage message, CanConfirmationBudget budget, CancellationToken ct, Action? entering = null)
    {
        _ = budget.Remaining(ct);
        entering?.Invoke();
        if (transmitter is ICanBudgetedConfirmedTransmitter configured) configured.WriteConfirmed(message, budget, ct);
        else transmitter.WriteConfirmed(message, ct); // 兼容既有提供者；外层仍追踪实际退出和软期限。
    }

    internal ReceiveSnapshot Snapshot(FeedbackSubscription subscription, bool running)
    {
        var stats = _router.Diagnostics;
        var error = stats.Error ?? _isolation;
        var frames = subscription.Snapshot();
        if (_raw is ICanClockValidity clock)
            frames = frames.Select(f => clock.IsClockCurrent(f.ClockId) ? f : f with
            { Quality = "TimeUnknown", AgeUpperBoundMs = null, FreshnessKnown = false }).ToArray();
        if (error != null) frames = frames.Select(f => f with { Quality = "SourceFault" }).ToArray();
        return new(Epoch, subscription.Epoch, running && !_closing && error == null,
            frames, stats.Depth, stats.Evictions, subscription.Replacements,
            (_raw as ICanQueueStatistics)?.GetQueueStatistics() ?? new(null, null, null, null), stats.Error ?? _isolation);
    }

    public void Isolate(string reason)
    {
        CanopenCyclicService[] services;
        CanBackgroundJob[] jobs;
        lock (_gate) { _isolation ??= reason; services = _services.Values.ToArray(); jobs = _jobs.Values.ToArray(); }
        foreach (var service in services) service.RequestStop();
        foreach (var job in jobs) job.Dispose();
    }

    public CanBackgroundJob StartJob(string name, Func<CancellationToken, Task> work)
    {
        lock (_gate)
        {
            CheckLocked();
            if (_senders.Values.Any(claim => claim.Diagnostic) || _startingSenders.Values.Any(diagnostic => diagnostic) || _jobs.ContainsKey(name))
                throw new InvalidOperationException("周期任务已存在或有序发送正在启动/占用连接，请先停止");
            var job = new CanBackgroundJob(work);
            _jobs.Add(name, job);
            return job;
        }
    }
    public async Task StopJobAsync(string name, int timeoutMs, CancellationToken ct)
        => _ = await StopJobWithDiagnosticsAsync(name, timeoutMs, ct).ConfigureAwait(false);

    internal async Task<string?> StopJobWithDiagnosticsAsync(string name, int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        ct.ThrowIfCancellationRequested();
        CanBackgroundJob job;
        lock (_gate) if (!_jobs.TryGetValue(name, out job!)) throw new InvalidOperationException($"此连接不存在周期任务 {name}");
        try { await job.StopAsync(timeoutMs, ct).ConfigureAwait(false); }
        catch { if (job.HasPendingNativeWrites) Isolate($"周期任务 {name} 的原生写调用尚未退出"); throw; }
        lock (_gate)
            if (_jobs.TryGetValue(name, out var current) && ReferenceEquals(current, job)) _jobs.Remove(name);
        return job.StopWarning;
    }

    public async Task CloseAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        long started = Stopwatch.GetTimestamp();
        await ClosePhysicalAsync(timeoutMs, ct).ConfigureAwait(false);
        int remaining = Math.Max(0, timeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
        await WaitForWorkersAsync(remaining, ct).ConfigureAwait(false);
    }
    internal Task ClosePhysicalAsync(int timeoutMs, CancellationToken ct = default)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        return WaitCloseAsync(RequestClose(), timeoutMs, ct);
    }
    internal Task RequestClose()
    {
        Task task;
        lock (_gate)
        {
            if (_closed) return _closeTask ?? Task.CompletedTask;
            _closing = true;
            _foregroundWait.RequestStop();
            if (!_foregroundStop.IsCancellationRequested) _foregroundCancellation = _foregroundStop.CancelAsync();
            if (_closeTask?.IsFaulted == true || _closeTask?.IsCanceled == true) _closeTask = null;
            if (_closeTask == null)
            {
                using (ExecutionContext.SuppressFlow()) _closeTask = Task.Run(async () =>
                {
                    try { await CloseCoreAsync().ConfigureAwait(false); }
                    catch (Exception ex) { lock (_gate) _closeError = $"{ex.GetType().Name}: {ex.Message}"; throw; }
                });
            }
            task = _closeTask;
        }
        _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        return task;
    }
    private async Task WaitCloseAsync(Task task, int timeoutMs, CancellationToken ct)
    {
        try { await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            if (!task.IsCompleted) Isolate("物理关闭等待结束，原生 I/O、回调或受控停机尚未实际退出；句柄保持占用");
            if (ex is TimeoutException && !task.IsCompleted)
                throw new TimeoutException($"CAN 收尾超过 {timeoutMs} ms。{GetLifecycleDiagnostics()}", ex);
            throw;
        }
    }
    private async Task CloseCoreAsync()
    {
        CanopenCyclicService[] services;
        CanBackgroundJob[] jobs;
        var cleanupErrors = new List<Exception>();
        lock (_gate) { services = _services.Values.ToArray(); jobs = _jobs.Values.ToArray(); }
        Phase("停止后台目标与周期任务");
        foreach (var service in services) service.FreezeForClose();
        foreach (var job in jobs) job.Dispose();
        RetainWorkers(jobs);
        // Actual I/O stays in _activeIo; all real cancellation callbacks are joined.
        // A managed expression/log cannot hold an otherwise released driver. Its
        // worker remains strongly retained and old admission is permanently frozen.
        try { await Task.WhenAll(jobs.Select(j => j.CancellationCompletion)).ConfigureAwait(false); }
        catch (Exception ex) { cleanupErrors.Add(ex); }
        Phase("确认有序服务退出与发送收尾");
        foreach (var service in services)
        {
            // The service joins its actual worker before reporting historical or
            // stop errors. Those errors must not skip other owners or SDK Close.
            try { await service.CloseForConnectionAsync().ConfigureAwait(false); }
            catch (Exception ex) { cleanupErrors.Add(ex); }
        }
        Phase("等待 CANopen 接收任务退出");
        try { await _receiveStop.CancelAsync().ConfigureAwait(false); }
        catch (Exception ex) { cleanupErrors.Add(ex); }
        if (_receiveTask != null) await _receiveTask.ConfigureAwait(false);
        Phase("等待实际 I/O 与原生打开调用退出");
        try { await _foregroundCancellation.ConfigureAwait(false); }
        catch (Exception ex) { cleanupErrors.Add(ex); }
        while (true)
        {
            lock (_gate) if (_activeIo == 0) break;
            await Task.Delay(10).ConfigureAwait(false);
        }
        lock (_gate) if (_closed) return; // Failed Open may already have released this exact owner.
        if (Volatile.Read(ref _nativeCancellationError) is { } cancellationError) cleanupErrors.Add(cancellationError);
        Phase("驱动 Close/Dispose（含 NI 接收泵、发送确认与 Stop/Clear）");
        bool closeSucceeded = false;
        try { _raw.Close(); _raw.Dispose(); closeSucceeded = true; }
        catch (Exception ex) { cleanupErrors.Add(ex); }
        finally
        {
            // A historical RX error can be reported after native handles were successfully released.
            if (closeSucceeded || _raw is ICanCloseState { HandlesReleased: true })
            {
                MarkClosed();
            }
        }
        var failures = jobs.Select(j => j.Error)
            .Concat(services.Select(s => s.Snapshot().Cycle.Error))
            .Append(_router.Diagnostics.Error).Where(error => error != null).ToArray();
        if (failures.Length != 0) cleanupErrors.Add(new InvalidOperationException("CAN 后台通信任务曾失败：" + string.Join(Environment.NewLine, failures)));
        if (cleanupErrors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupErrors[0]).Throw();
        if (cleanupErrors.Count > 1) throw new AggregateException(IsClosed
            ? "CAN 句柄已释放，但收尾存在通信、取消回调或驱动错误"
            : "CAN 最终关闭未确认；保留物理句柄与隔离，收尾同时存在通信、回调或驱动失败，可重试关闭", cleanupErrors);
    }
    private void Phase(string phase) { lock (_gate) _closePhase = phase; }
    internal async Task WaitForWorkersAsync(int timeoutMs, CancellationToken ct = default)
    {
        CanBackgroundJob[] jobs;
        lock (_gate) jobs = _jobs.Values.ToArray();
        var completion = Task.WhenAll(jobs.Select(j => j.Completion));
        if (!completion.IsCompleted)
        {
            try
            {
                if (timeoutMs <= 0 && timeoutMs != Timeout.Infinite) throw new TimeoutException();
                await completion.WaitAsync(timeoutMs == Timeout.Infinite ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                const string reason = "CAN 物理句柄已释放，但普通后台任务尚未退出；旧所有者保持冻结并保留任务，不能报告完全停止";
                lock (_gate) _closeError = reason;
                throw new TimeoutException(reason + "。" + GetLifecycleDiagnostics(), ex);
            }
        }
        await completion.ConfigureAwait(false);
        var errors = jobs.Select(j => j.Error).Where(e => e != null).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException("CAN 物理句柄已释放，但后台任务曾失败：" + string.Join(Environment.NewLine, errors));
    }
    private void RetainWorkers(CanBackgroundJob[] jobs)
    {
        var completion = Task.WhenAll(jobs.Select(j => j.Completion));
        RetiredWorkers.TryAdd(this, completion);
        _ = completion.ContinueWith(t =>
        {
            string? failure = t.Exception?.ToString() ?? jobs.FirstOrDefault(j => j.Error != null)?.Error;
            if (failure != null) lock (_gate) _closeError = failure;
            RetiredWorkers.TryRemove(this, out _);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    private void MarkClosed()
    {
        lock (_gate) { _closed = true; _closePhase = "驱动句柄已释放"; }
        ReleasePhysicalClaim();
    }
    private void ReleasePhysicalClaim() =>
        ((ICollection<KeyValuePair<string, CanConnectionResource>>)PhysicalOwners).Remove(new(_physicalKey, this));
    public string GetLifecycleDiagnostics()
    {
        lock (_gate) return $"连接={Epoch}; 物理通道={_physicalKey}; 关闭阶段={_closePhase}; 活动I/O={_activeIo}; " +
            $"已关闭={_closed}; 后台未退出={HasPendingManagedWorkers}; 隔离={_isolation ?? "无"}; 最近关闭错误={_closeError ?? "无"}";
    }
    public void Close() => CloseAsync().GetAwaiter().GetResult();
    public void Dispose()
    {
        // 同步资源回收不占住宿主线程；显式 CAN_Close 才确认退出。
        var actualClose = RequestClose();
        _ = Task.Run(() => ObserveCloseAsync(actualClose), CancellationToken.None);
    }
    private async Task ObserveCloseAsync(Task actualClose)
    {
        // This observer owns no caller wait. The service already has its configured
        // stop deadline; a legacy 5 s soft wait must not cancel an allowed 8 s stop.
        try
        {
            await actualClose.ConfigureAwait(false);
            // This observer owns no physical lease and has no user wait budget.
            // Keep observing actual managed exit without declaring HostExited early.
            await WaitForWorkersAsync(Timeout.Infinite).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"CAN 自动收尾未确认：{GetLifecycleDiagnostics()}; {ex}");
            try { _reportCloseError?.Invoke(ex); }
            catch (Exception logError) { Trace.TraceError($"CAN 收尾日志回调失败：{logError}"); }
        }
    }
    public string GetReceiveDiagnostics() => GetLifecycleDiagnostics() + $"; 共享接收={Controlled}; {_router.FanoutDiagnostics}; " +
        (_raw as ICanAdapterDiagnostics)?.GetReceiveDiagnostics();
}

public sealed class CanBackgroundJob
{
    private static readonly AsyncLocal<CanBackgroundJob?> Ambient = new();
    internal static CanBackgroundJob? Current => Ambient.Value;
    private readonly NativeWriteLifetime _nativeWrites = new();
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _work;
    private Task _callbacks = Task.CompletedTask;
    public Task Completion => JoinAsync();
    internal bool HasPendingNativeWrites => _nativeWrites.HasPending;
    internal Task CancellationCompletion { get { lock (_gate) return _callbacks; } }
    private string? _error;
    public string? Error => Volatile.Read(ref _error);
    internal string? StopWarning
    {
        get
        {
            lock (_gate)
            {
                var callbackError = _callbacks.Exception?.ToString();
                return Error == null ? callbackError : callbackError == null ? Error : Error + Environment.NewLine + callbackError;
            }
        }
    }
    internal CanBackgroundJob(Func<CancellationToken, Task> work)
    {
        ExecutionScopedExecutor.Own(Dispose);
        _work = Task.Run(async () =>
        {
            var previous = Ambient.Value;
            Ambient.Value = this;
            try { await work(_stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (Exception ex) { Volatile.Write(ref _error, ex.ToString()); Dispose(); }
            finally { Ambient.Value = previous; }
        });
    }
    internal IDisposable EnterNativeWrite() => _nativeWrites.Enter();
    private async Task JoinAsync()
    {
        await _work.ConfigureAwait(false);
        Task callbacks;
        lock (_gate) callbacks = _callbacks;
        await Task.WhenAll(callbacks, _nativeWrites.Completion).ConfigureAwait(false);
    }
    public void Dispose()
    {
        lock (_gate)
        {
            _nativeWrites.Freeze();
            if (!_stop.IsCancellationRequested) _callbacks = _stop.CancelAsync();
        }
    }
    public async Task StopAsync(int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        ct.ThrowIfCancellationRequested();
        long started = Stopwatch.GetTimestamp();
        Dispose();
        var completion = Completion;
        var exited = completion.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        double remaining = timeoutMs - Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (remaining <= 0) throw new TimeoutException($"CAN 周期停止超过 {timeoutMs} ms，实际退出仍被追踪");
        await exited.WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
            throw new TimeoutException($"CAN 周期停止已超过总期限 {timeoutMs} ms；真实退出结果仍保留");
    }
}
