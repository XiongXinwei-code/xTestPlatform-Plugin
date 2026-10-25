using System.Collections.Concurrent;
using System.Diagnostics;
using CAN.Adapters;
using CAN.Models;

namespace CAN.Runtime;

/// <summary>连接所有者：真实 I/O 与后台任务退出前不释放驱动句柄。</summary>
public sealed class CanConnectionResource : ICanAdapter, ICanAdapterDiagnostics
{
    private static readonly ConcurrentDictionary<string, CanConnectionResource> PhysicalOwners = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly object _txGate = new();
    private readonly ICanAdapter _raw;
    private readonly string _physicalKey;
    private readonly CancellationTokenSource _receiveStop = new();
    private readonly CancellationTokenSource _foregroundStop = new();
    private readonly Action<Exception>? _reportCloseError;
    private readonly CanReceiveRouter _router = new();
    private readonly Dictionary<string, CanopenCyclicService> _services = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CanBackgroundJob> _jobs = new(StringComparer.Ordinal);
    private readonly HashSet<uint> _claimedIds = [];
    private CanopenCyclicService? _sender;
    private readonly AsyncLocal<CanopenCyclicService?> _ownerCommand = new();
    private readonly AsyncLocal<CancellationToken> _ownerCommandToken = new();
    private Task? _receiveTask, _closeTask;
    private Task _foregroundCancellation = Task.CompletedTask;
    private int _activeIo;
    private bool _controlled, _closing, _closed, _rawProtocolClaimed;
    private CanProtocolType? _openedProtocol;
    private string? _isolation;
    private string _closePhase = "运行中";
    private string? _closeError;
    public Guid Epoch { get; } = Guid.NewGuid();
    public bool SupportsConfirmedTransmit => _raw is ICanConfirmedTransmitter { ConfirmationAvailable: true };
    internal void VerifyConfirmationAvailable(int timeoutMs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        long started = Stopwatch.GetTimestamp();
        var task = Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException("发送能力预检在实际提交前已超时");
            lock (_gate) { CheckLocked(); _activeIo++; }
            try { (_raw as ICanConfirmedTransmitter)?.VerifyConfirmationAvailable(); }
            finally { lock (_gate) _activeIo--; }
        });
        try { task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).GetAwaiter().GetResult(); }
        catch
        {
            if (!task.IsCompleted) Isolate("发送能力预检尚未实际退出");
            _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            throw;
        }
    }
    internal Task<bool> ConfirmTransmitQuiescentAsync() => Task.Run(() =>
    {
        lock (_txGate) return _raw is ICanTransmitQuiescence capability && capability.ConfirmTransmitQuiescent();
    });
    public bool IsConnected { get { lock (_gate) return !_closed && _raw.IsConnected &&
        (!_closing || (_ownerCommand.Value != null && ReferenceEquals(_sender, _ownerCommand.Value))); } }
    public bool Controlled { get { lock (_gate) return _controlled; } }
    public bool IsClosed { get { lock (_gate) return _closed; } }
    internal bool IsClosing { get { lock (_gate) return _closing; } }
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
            try { await previous.CloseAsync(remaining, ct).ConfigureAwait(false); }
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

    public async Task OpenAsync(CanAdapterConfig config, int timeoutMs, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs); ct.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        void CheckBudget()
        {
            ct.ThrowIfCancellationRequested();
            if (clock.ElapsedMilliseconds >= timeoutMs) throw new TimeoutException($"CAN 打开总期限 {timeoutMs} ms 已到");
        }
        var actual = Task.Run(() => OpenCore(config, CheckBudget));
        try
        {
            await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
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
        if (_closed || (_closing && !finishingOwnedBatch && (_ownerCommand.Value == null || !ReferenceEquals(_sender, _ownerCommand.Value))))
            throw new InvalidOperationException("CAN 连接正在关闭或已关闭");
        if (_isolation != null) throw new InvalidOperationException($"CAN 连接已隔离：{_isolation}");
        if (!_raw.IsConnected) throw new InvalidOperationException("CAN 连接未打开");
    }

    public CanopenCyclicService StartService(string name, FeedbackRoute[] routes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("服务名称不能为空");
        lock (_gate)
        {
            CheckLocked();
            if (_services.ContainsKey(name)) throw new InvalidOperationException($"CANopen 服务 {name} 已存在，请先确认停止");
            if (_rawProtocolClaimed) throw new InvalidOperationException("该连接已供 UDS/XCP 使用，请关闭后以 CANopen 模式重新打开");
            if (!_controlled && (_activeIo != 0 || _jobs.Count != 0))
                throw new InvalidOperationException("存在正在执行的原始 I/O 或通用周期任务，不能切换接收模式");
            using var transition = !_controlled ? CAN.CANopen.CanopenConnectionAccess.For(this).EnterControlledMode(_router.Publish) : null;
            var subscription = _router.Subscribe(routes);
            var service = new CanopenCyclicService(this, subscription);
            _services.Add(name, service);
            if (!_controlled)
            {
                _controlled = true;
                _receiveTask = Task.Run(ReceiveLoop);
            }
            return service;
        }
    }

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

    public Task<CanMessage?> ReadCanopenAsync(uint id, int timeoutMs, CancellationToken ct)
    {
        lock (_gate) CheckLocked();
        return _router.ReadAsync(id, timeoutMs, ct);
    }

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
        long started = Stopwatch.GetTimestamp();
        var actual = Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            int remaining = timeoutMs == Timeout.Infinite ? Timeout.Infinite :
                timeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (timeoutMs != Timeout.Infinite && remaining <= 0) throw new TimeoutException("CAN 接收在提交前已超时");
            return ReadRaw(id, remaining, ct);
        }, CancellationToken.None);
        try
        {
            var result = await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (timeoutMs != Timeout.Infinite && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"CAN 接收结果返回时已超过配置期限 {timeoutMs} ms，不能发布迟到结果");
            return result;
        }
        catch
        {
            _ = actual.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            if (!actual.IsCompleted) Isolate("原始接收已停止等待但实际调用尚未退出");
            throw;
        }
    }
    private CanMessage? ReadRaw(uint? id, int timeoutMs, CancellationToken ct)
    {
        if (_ownerCommand.Value == null) CanExecutionGuard.CheckSubmission();
        lock (_gate)
        {
            CheckLocked();
            if (_controlled) throw new InvalidOperationException("受控 CANopen 已占用接收入口，请使用 CANopen FIFO/快照步骤");
            _activeIo++;
        }
        try
        {
            // Do not stop the controlled RX/echo pump here; only cancel foreground raw readers.
            // Owner-controlled shutdown commands keep their own cancellation budget.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,
                _ownerCommand.Value == null ? _foregroundStop.Token : CancellationToken.None);
            if (_ownerCommand.Value == null) CanExecutionGuard.CheckSubmission();
            return id.HasValue ? _raw.Read(id.Value, timeoutMs, linked.Token) : _raw.Read(timeoutMs, linked.Token);
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
        var actual = Task.Run(() => WriteCore(copy, ct, started, timeoutMs), CancellationToken.None);
        try
        {
            await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"CAN 发送结果返回时已超过配置期限 {timeoutMs} ms；原始提交可能已经完成，不能视为撤回");
        }
        catch (Exception ex)
        {
            _ = actual.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            if (!actual.IsCompleted) Isolate($"原始发送停止等待但实际 I/O 尚未退出，配置期限={timeoutMs} ms；不能假设报文已撤回");
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

    private void WriteCore(CanMessage copy, CancellationToken callerToken, long started, int timeoutMs)
    {
        CheckSubmission();
        CAN.Helpers.CanHelper.ValidateTransmitFrame(copy);
        // Reject an already isolated/closing owner without waiting for a native
        // submission that may still hold the transmit gate. Recheck after the
        // gate is acquired to cover a concurrent close/isolation transition.
        lock (_gate) CheckLocked();
        lock (_txGate)
        {
            lock (_gate)
            {
                // Check the original token and monotonic deadline, not cancellation
                // callbacks or the outer waiter's continuation, before admitting I/O.
                CheckSubmission();
                CheckLocked();
                if (copy.IsFd && _openedProtocol == CanProtocolType.Classic)
                    throw new ArgumentException("连接以 Classic CAN 打开，不能发送 CAN FD 帧；请使用 FD 配置重新打开，不能自动降级或截断数据");
                if (_claimedIds.Contains(copy.Id)) throw new InvalidOperationException($"COB-ID 0x{copy.Id:X} 已由有序周期服务占用");
                _activeIo++;
            }
            try
            {
                if (_ownerCommand.Value != null && _raw is ICanConfirmedTransmitter confirmed)
                    confirmed.WriteConfirmed(copy, _ownerCommandToken.Value);
                else _raw.Write(copy);
            }
            finally { lock (_gate) _activeIo--; }
        }

        void CheckSubmission()
        {
            if (_ownerCommand.Value == null) CanExecutionGuard.CheckSubmission();
            callerToken.ThrowIfCancellationRequested();
            if (timeoutMs > 0 && System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"CAN 发送排队已超过配置期限 {timeoutMs} ms，未提交此帧");
        }
    }

    internal void ReserveSender(CanopenCyclicService service, uint[] ids)
    {
        lock (_gate)
        {
            CheckLocked();
            if (_sender != null) throw new InvalidOperationException("此连接已有周期发送所有者，或旧发送尚未确认退出");
            if (_jobs.Count != 0) throw new InvalidOperationException("请先停止通用周期任务后再启动有序周期发送");
            _sender = service;
            _claimedIds.UnionWith(ids);
        }
    }
    internal void ReleaseSender(CanopenCyclicService service)
    {
        lock (_gate) if (ReferenceEquals(_sender, service)) { _sender = null; _claimedIds.Clear(); }
    }

    internal void UpdateSender(CanopenCyclicService service, uint[] ids)
    {
        lock (_gate)
        {
            CheckLocked();
            if (!ReferenceEquals(_sender, service)) throw new InvalidOperationException("SYNC 所有者已失效");
            _claimedIds.Clear(); _claimedIds.UnionWith(ids);
        }
    }

    internal async Task RunOwnerCommandAsync(CanopenCyclicService owner, Func<Task> command, CancellationToken ct)
    {
        lock (_gate) if (!ReferenceEquals(_sender, owner)) throw new InvalidOperationException("控制所有者已失效");
        // Do not hold _txGate across SDO: its Write runs on another task and requires that gate.
        var previous = _ownerCommand.Value; var previousToken = _ownerCommandToken.Value;
        _ownerCommand.Value = owner; _ownerCommandToken.Value = ct;
        try { await command().ConfigureAwait(false); }
        finally { _ownerCommand.Value = previous; _ownerCommandToken.Value = previousToken; }
    }

    internal void SendBatch(CanopenCyclicService owner, OrderedFrame[] frames, bool confirmed, CancellationToken ct,
        Action<bool> submitted)
    {
        // Validate the whole immutable batch before any member can enter the driver.
        foreach (var frame in frames)
            CAN.Helpers.CanHelper.ValidateTransmitFrame(new CanMessage { Id = frame.CobId, Data = frame.Data });
        lock (_txGate)
        {
            foreach (var frame in frames)
            {
                owner.CheckSending(); // 不依赖异步取消回调何时传递到 linked token。
                ct.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    CheckLocked(finishingOwnedBatch: ReferenceEquals(_sender, owner));
                    if (!ReferenceEquals(_sender, owner)) throw new InvalidOperationException("发送所有权已失效");
                    _activeIo++;
                }
                try
                {
                    var message = new CanMessage { Id = frame.CobId, Data = frame.Data.ToArray() };
                    submitted(false); // 进入原生调用即视为可能已提交，异常也不能假定未发送。
                    if (confirmed) ((ICanConfirmedTransmitter)_raw).WriteConfirmed(message, ct);
                    else _raw.Write(message);
                    if (confirmed) submitted(true);
                }
                finally { lock (_gate) _activeIo--; }
            }
        }
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
            if (_sender != null || _jobs.ContainsKey(name)) throw new InvalidOperationException("周期任务已存在或有序发送正在占用连接，请先停止");
            var job = new CanBackgroundJob(work);
            _jobs.Add(name, job);
            return job;
        }
    }
    public async Task StopJobAsync(string name, int timeoutMs, CancellationToken ct)
    {
        CanBackgroundJob job;
        lock (_gate) if (!_jobs.TryGetValue(name, out job!)) throw new InvalidOperationException($"此连接不存在周期任务 {name}");
        try { await job.StopAsync(timeoutMs, ct).ConfigureAwait(false); }
        catch { if (!job.Completion.IsCompleted) Isolate($"周期任务 {name} 尚未退出"); throw; }
        lock (_gate)
            if (_jobs.TryGetValue(name, out var current) && ReferenceEquals(current, job)) _jobs.Remove(name);
    }

    public Task CloseAsync(int timeoutMs = 5000, CancellationToken ct = default)
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
            if (!_foregroundStop.IsCancellationRequested) _foregroundCancellation = _foregroundStop.CancelAsync();
            if (_closeTask?.IsFaulted == true || _closeTask?.IsCanceled == true) _closeTask = null;
            task = _closeTask ??= Task.Run(async () =>
            {
                try { await CloseCoreAsync().ConfigureAwait(false); }
                catch (Exception ex) { lock (_gate) _closeError = $"{ex.GetType().Name}: {ex.Message}"; throw; }
            });
        }
        _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        return task;
    }
    private async Task WaitCloseAsync(Task task, int timeoutMs, CancellationToken ct)
    {
        try { await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            if (!task.IsCompleted) Isolate("关闭等待结束，后台任务或原生 I/O 尚未实际退出；句柄保持占用");
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
        // A faulted cancellation callback is diagnostic, not proof that a task is still running.
        // Await all real workers before proceeding; keep their failures for the final close result.
        try { await Task.WhenAll(jobs.Select(j => j.Completion)).ConfigureAwait(false); }
        catch (Exception ex) { cleanupErrors.Add(ex); }
        Phase("确认有序服务退出与发送收尾");
        foreach (var service in services) await service.CloseForConnectionAsync().ConfigureAwait(false);
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
        Phase("驱动 Close/Dispose（含 NI 接收泵、发送确认与 Stop/Clear）");
        try { _raw.Close(); _raw.Dispose(); }
        finally
        {
            // A historical RX error can be reported after native handles were successfully released.
            if (_raw is ICanCloseState { HandlesReleased: true })
            {
                MarkClosed();
            }
        }
        MarkClosed();
        var failures = jobs.Select(j => j.Error)
            .Concat(services.Select(s => s.Snapshot().Cycle.Error))
            .Append(_router.Diagnostics.Error).Where(error => error != null).ToArray();
        if (failures.Length != 0) cleanupErrors.Add(new InvalidOperationException("CAN 句柄已关闭，但后台通信任务曾失败：" + string.Join(Environment.NewLine, failures)));
        if (cleanupErrors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupErrors[0]).Throw();
        if (cleanupErrors.Count > 1) throw new AggregateException("CAN 句柄已释放，但收尾存在通信或取消回调错误", cleanupErrors);
    }
    private void Phase(string phase) { lock (_gate) _closePhase = phase; }
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
            $"已关闭={_closed}; 隔离={_isolation ?? "无"}; 最近关闭错误={_closeError ?? "无"}";
    }
    public void Close() => CloseAsync().GetAwaiter().GetResult();
    public void Dispose()
    {
        // 同步资源回收不占住宿主线程；显式 CAN_Close 才确认退出。
        _ = ObserveCloseAsync();
    }
    private async Task ObserveCloseAsync()
    {
        try { await CloseAsync().ConfigureAwait(false); }
        catch (Exception ex)
        {
            Trace.TraceError($"CAN 自动收尾未确认：{GetLifecycleDiagnostics()}; {ex}");
            try { _reportCloseError?.Invoke(ex); }
            catch (Exception logError) { Trace.TraceError($"CAN 收尾日志回调失败：{logError}"); }
        }
    }
    public string GetReceiveDiagnostics() => GetLifecycleDiagnostics() + $"; 受控={Controlled}; " +
        (_raw as ICanAdapterDiagnostics)?.GetReceiveDiagnostics();
}

public sealed class CanBackgroundJob
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _work;
    private Task _callbacks = Task.CompletedTask;
    public Task Completion { get { lock (_gate) return Task.WhenAll(_work, _callbacks); } }
    public string? Error { get; private set; }
    internal CanBackgroundJob(Func<CancellationToken, Task> work)
    {
        _work = Task.Run(async () =>
        {
            try { await work(_stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (Exception ex) { Error = ex.ToString(); }
        });
    }
    public void Dispose()
    {
        lock (_gate) if (!_stop.IsCancellationRequested) _callbacks = _stop.CancelAsync();
    }
    public async Task StopAsync(int timeoutMs, CancellationToken ct)
    {
        Dispose();
        await Completion.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
        if (Error != null) throw new InvalidOperationException($"后台周期发送失败：{Error}");
    }
}
