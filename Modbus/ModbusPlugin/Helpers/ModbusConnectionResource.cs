using NModbus;
using PluginCommon;
using System.Diagnostics;

namespace Modbus.Helpers;

/// <summary>连接及其全部事务的唯一所有者。停止等待不代表写入已经取消。</summary>
public sealed class ModbusConnectionResource : IDisposable
{
    private readonly PendingIoGate _io = new();
    private readonly object _state = new();
    private readonly CancellationTokenSource _lifetime = new();
    private IDisposable? _transport;
    private IModbusMaster? _master;
    private bool _ready, _closing;
    private readonly Action<Exception>? _report;
    private bool _masterReleased, _transportReleased;
    private Task<Exception?>? _shutdown;
    private Task? _closeObserver;
    private bool _releasing, _releaseCompleted;
    public IModbusMaster Master => _master ?? throw new InvalidOperationException("Modbus 连接尚在建立，Master 尚不可用");
    public int TimeoutMs { get; }
    public string? PhysicalResourceKey { get; }
    public bool IsClosed => _io.IsClosed;
    internal Task? CloseCompletion => _io.CloseCompletion;
    internal bool IsClosing { get { lock (_state) return _closing; } }

    public ModbusConnectionResource(int timeoutMs, Action<Exception>? report = null, string? physicalResourceKey = null)
        : this(timeoutMs, report, physicalResourceKey, registerStepOwnership: true) { }

    private ModbusConnectionResource(int timeoutMs, Action<Exception>? report, string? physicalResourceKey, bool registerStepOwnership)
    {
        TimeoutMs = timeoutMs > 0 ? timeoutMs : throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        _report = report;
        PhysicalResourceKey = physicalResourceKey;
        if (registerStepOwnership) ExecutionScopedExecutor.Own(Dispose);
    }
    public ModbusConnectionResource(IModbusMaster master, IDisposable transport, int timeoutMs, Action<Exception>? report = null, string? physicalResourceKey = null)
        : this(timeoutMs, report, physicalResourceKey, registerStepOwnership: false)
    { _master = master; _transport = transport; _ready = true; ExecutionScopedExecutor.Own(Dispose); }
    internal void OwnTransport(IDisposable transport) => _transport = transport;
    internal void OwnMaster(IModbusMaster master) => _master = master;
    internal Task InitializeAsync(Func<ModbusConnectionResource, CancellationToken, Task> factory, CancellationToken ct, int? remainingTimeoutMs = null)
    {
        int totalTimeoutMs = remainingTimeoutMs ?? TimeoutMs;
        long startedAt = Stopwatch.GetTimestamp();
        lock (_state) { if (_closing || _ready) throw new InvalidOperationException("Modbus 建立已终止或资源已经就绪"); }
        return _io.RunAsync(async () =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            linked.CancelAfter(RemainingOpen());
            lock (_state) { if (_closing) throw new InvalidOperationException("Modbus 建立已被断开请求冻结"); }
            linked.Token.ThrowIfCancellationRequested();
            if (PhysicalResourceKey != null)
                await RetiringResources.WaitForPreviousAsync(PhysicalResourceKey, RemainingOpen(), linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested(); RemainingOpen();
            await factory(this, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested(); RemainingOpen();
            lock (_state)
            {
                if (_closing) throw new InvalidOperationException("Modbus 建立期间收到断开请求，拒绝迟到发布");
                if (_master == null || _transport == null) throw new InvalidOperationException("Modbus 建立没有提供完整资源");
                _ready = true;
            }
        }, totalTimeoutMs, "Modbus 连接建立", ct);
        int RemainingOpen()
        {
            double remaining = totalTimeoutMs - Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (remaining <= 0) throw new TimeoutException("Modbus 建立总期限已到（包含旧物理连接收尾等待）");
            return (int)Math.Ceiling(remaining);
        }
    }
    private void RequireReady(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_state) if (!_ready || _closing) throw new InvalidOperationException("Modbus 连接尚未就绪或正在关闭");
    }

    internal void EnsureAvailableForPublication()
    {
        lock (_state)
            if (!_ready || _closing || _io.IsClosed || _io.IsolatedReason != null)
                throw new InvalidOperationException("Modbus 连接已关闭、正在收尾或被隔离，拒绝发布迟到结果");
    }

    public Task<T> WithTimeoutAsync<T>(Func<Task<T>> operation, int timeoutMs, string description, CancellationToken ct)
    { RequireReady(ct); return _io.RunAsync(operation, timeoutMs, description, ct); }
    public Task WithTimeoutAsync(Func<Task> operation, int timeoutMs, string description, CancellationToken ct)
    { RequireReady(ct); return _io.RunAsync(operation, timeoutMs, description, ct); }
    public async Task CloseAsync(int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var shutdown = RequestShutdown();
        TrackRetirement();
        // Freeze admission immediately. Both the driver interrupt and cancellation
        // callbacks run off the caller; release must await their real completion.
        var closing = _io.CloseAsync(ReleaseAfterShutdown, timeoutMs, ct);
        ObserveRelease();
        try { await closing.ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (shutdown.IsCompletedSuccessfully && shutdown.Result is { } stopError)
        { throw new AggregateException("Modbus 关闭及停止请求失败", stopError, ex); }
        if (await shutdown.ConfigureAwait(false) is { } error)
            throw new InvalidOperationException("Modbus 已收尾，但原始 I/O 取消或回调失败", error);
    }

    private Task<Exception?> RequestShutdown()
    {
        lock (_state)
        {
            _closing = true;
            // A completed failed interrupt may be retried while I/O is pending.
            // Once physical release starts, another abort must not touch that handle.
            if (_shutdown == null || (!_releasing && !_releaseCompleted &&
                _shutdown.IsCompletedSuccessfully && _shutdown.Result != null))
                _shutdown = Task.Run(ShutdownCoreAsync, CancellationToken.None);
            return _shutdown;
        }
    }

    private async Task<Exception?> ShutdownCoreAsync()
    {
        var failures = new List<Exception>();
        Task callbacks;
        try { callbacks = _lifetime.CancelAsync(); }
        catch (Exception ex) { callbacks = Task.FromException(ex); }
        // An arbitrary transport implementation may block in its abort operation.
        // Start it independently so a slow callback cannot prevent the I/O unblock.
        var interrupt = Task.Run(InterruptPendingIo, CancellationToken.None);
        try { await callbacks.ConfigureAwait(false); } catch (Exception ex) { failures.Add(ex); }
        try { if (await interrupt.ConfigureAwait(false) is { } error) failures.Add(error); }
        catch (Exception ex) { failures.Add(ex); }
        return failures.Count switch { 0 => null, 1 => failures[0], _ => new AggregateException("Modbus 停止请求失败", failures) };
    }

    // PendingIoGate invokes this only on its actual background close worker, after
    // admitted I/O exits. Failed callbacks have exited too; keep their diagnostics
    // without permanently preventing an otherwise successful physical release.
    private void ReleaseAfterShutdown()
    {
        Task<Exception?> shutdown;
        lock (_state)
        {
            _releasing = true;
            shutdown = _shutdown ??= Task.Run(ShutdownCoreAsync, CancellationToken.None);
        }
        try
        {
            _ = shutdown.GetAwaiter().GetResult();
            Release();
            lock (_state) _releaseCompleted = true;
        }
        finally { lock (_state) _releasing = false; }
    }
    private Exception? InterruptPendingIo()
    {
        // 不能依赖 pending 快照：已获准的 Task.Run 可能尚未发布 _pending。
        // 永久冻结本代传输 token，使该窗口内迟到启动的原生 I/O 也立即退出。
        if (_transport is not ICancellableModbusTransport transport) return null;
        try { transport.CancelPendingIo(); }
        catch (ObjectDisposedException) when (!_io.HasPendingOperation)
        { System.Diagnostics.Trace.TraceInformation("Modbus socket 已由已退出的事务完成收尾"); }
        catch (Exception ex) { return ex; }
        return null;
    }
    private void Release()
    {
        var failures = new List<Exception>();
        try { if (!_masterReleased) { _master?.Dispose(); _masterReleased = true; } } catch (Exception ex) { failures.Add(ex); }
        try { if (!_transportReleased) { _transport?.Dispose(); _transportReleased = true; } } catch (Exception ex) { failures.Add(ex); }
        if (failures.Count != 0) throw new AggregateException("Modbus 连接释放失败", failures);
    }
    public void Dispose()
    {
        var shutdown = RequestShutdown();
        TrackRetirement();
        _io.RequestClose(ReleaseAfterShutdown);
        ObserveRelease();
        var actualClose = _io.CloseCompletion ?? Task.CompletedTask;
        lock (_state)
        {
            if (_closeObserver == null || _closeObserver.IsCompleted)
                _closeObserver = Task.Run(() => ObserveCloseAsync(actualClose, shutdown), CancellationToken.None);
        }
    }

    private async Task ObserveCloseAsync(Task actualClose, Task<Exception?> shutdown)
    {
        Exception? failure = null;
        try { await actualClose.ConfigureAwait(false); } catch (Exception ex) { failure = ex; }
        if (await shutdown.ConfigureAwait(false) is { } stopError)
            failure = failure == null ? stopError : new AggregateException("Modbus 后台收尾失败", failure, stopError);
        if (failure == null) return;
        Trace.TraceError($"Modbus 后台收尾诊断（已释放={IsClosed}）：{failure}");
        try { _report?.Invoke(failure); }
        catch (Exception ex) { Trace.TraceError($"Modbus 收尾日志回调失败：{ex}"); }
    }
    private void TrackRetirement()
    {
        if (PhysicalResourceKey != null && !_io.IsClosed)
            RetiringResources.Track(PhysicalResourceKey, this, CloseAsync, () => _io.IsClosed, () => _io.IsolatedReason ?? "Modbus 正在关闭");
    }
    private void ObserveRelease()
    {
        if (PhysicalResourceKey != null && _io.CloseCompletion is { } actual)
            _ = actual.ContinueWith(_ => RetiringResources.ForgetReleased(PhysicalResourceKey, this), CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
