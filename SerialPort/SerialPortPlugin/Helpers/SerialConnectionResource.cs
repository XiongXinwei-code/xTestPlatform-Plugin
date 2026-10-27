using System.Diagnostics;
using System.Text;
using PluginCommon;
using SysSerialPort = System.IO.Ports.SerialPort;

namespace SerialPort.Helpers;

public interface ISerialChannel : IDisposable
{
    bool IsOpen { get; }
    int ReadTimeout { get; set; }
    int WriteTimeout { get; set; }
    int Read(byte[] buffer, int offset, int count);
    void Write(byte[] bytes);
}

/// <summary>关闭专用中断能力；返回不代表原始 I/O 已退出或全部字节未发送。</summary>
public interface IInterruptibleSerialChannel
{
    void CancelPendingIo();
}

/// <summary>同一串口只有一个完整事务，余量帧归连接所有；取消不撤回已经发出的字节。</summary>
public sealed class SerialConnectionResource : IDisposable
{
    private const int MaxFrameBytes = 16 * 1024 * 1024;
    private readonly ISerialChannel _channel;
    private readonly PendingIoGate _io = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<byte> _buffer = new();
    private readonly Action<Exception>? _report;
    private readonly object _shutdownSync = new();
    private Task<Exception?>? _shutdown;
    private Task? _closeObserver;
    private bool _releasing, _releaseCompleted;
    private volatile bool _ready;
    private int _closing;
    public string? PhysicalResourceKey { get; }
    public bool IsOpen => _ready && Volatile.Read(ref _closing) == 0 && _channel.IsOpen && !_io.IsClosed;
    public int WriteTimeout => _channel.WriteTimeout;
    public bool IsClosed => _io.IsClosed;
    internal Exception? CloseWarning { get; private set; }
    internal Task? CloseCompletion => _io.CloseCompletion;
    internal bool IsClosing => Volatile.Read(ref _closing) != 0;

    internal void EnsureAvailableForPublication()
    {
        if (!_ready || Volatile.Read(ref _closing) != 0 || _io.IsClosed || _io.IsolatedReason != null)
            throw new InvalidOperationException("串口连接已关闭、正在收尾或被隔离，拒绝发布迟到结果");
    }

    public SerialConnectionResource(SysSerialPort port, Action<Exception>? report = null) : this(new SerialChannel(port), report, port.PortName) { _ready = false; }
    public SerialConnectionResource(ISerialChannel channel, Action<Exception>? report = null, string? physicalPort = null)
    {
        _channel = channel; _report = report; _ready = true;
        PhysicalResourceKey = physicalPort == null ? null : SerialPortHelper.GetPhysicalPortKey(physicalPort);
        ExecutionScopedExecutor.Own(Dispose);
    }
    public async Task OpenAsync(Action open, int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        long startedAt = Stopwatch.GetTimestamp();
        _ready = false;
        if (Volatile.Read(ref _closing) != 0) throw new InvalidOperationException("串口建立已被关闭请求冻结");
        await _io.RunAsync(async () =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            linked.CancelAfter(RemainingOpen());
            if (PhysicalResourceKey != null)
                await RetiringResources.WaitForPreviousAsync(PhysicalResourceKey, RemainingOpen(), linked.Token).ConfigureAwait(false);
            if (Volatile.Read(ref _closing) != 0) throw new InvalidOperationException("串口建立已被关闭请求冻结");
            linked.Token.ThrowIfCancellationRequested(); RemainingOpen();
            open();
            linked.Token.ThrowIfCancellationRequested(); RemainingOpen();
        }, timeoutMs, "打开串口", ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _closing) != 0 || _lifetime.IsCancellationRequested) throw new InvalidOperationException("串口打开期间收到关闭请求，不能迟到发布");
        _ready = true;
        int RemainingOpen()
        {
            double remaining = timeoutMs - Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (remaining <= 0) throw new TimeoutException("串口打开总期限已到（包含旧端口收尾等待）");
            return (int)Math.Ceiling(remaining);
        }
    }
    public Task WriteAsync(byte[] bytes, CancellationToken ct)
        => WriteAsync(bytes, ct, static () => { });
    internal Task WriteAsync(byte[] bytes, CancellationToken ct, Action ensureExecutionCurrent)
        => _io.RunBlockingAsync(() => { ensureExecutionCurrent(); _channel.Write(bytes); }, _channel.WriteTimeout, "串口写入", ct);
    public Task<byte[]> ReadFrameAsync(int count, string terminator, int timeoutMs, CancellationToken ct)
        => ReadFrameAsync(count, terminator, timeoutMs, ct, static () => { });
    internal Task<byte[]> ReadFrameAsync(int count, string terminator, int timeoutMs, CancellationToken ct, Action ensureExecutionCurrent)
        => _io.RunBlockingAsync(() => ReadFrame(count, terminator, timeoutMs, ct, ensureExecutionCurrent), Guard(timeoutMs), "串口读取", ct);
    public Task<byte[]> QueryAsync(byte[] bytes, int count, string terminator, int timeoutMs, CancellationToken ct)
        => QueryAsync(bytes, count, terminator, timeoutMs, ct, static () => { });
    internal Task<byte[]> QueryAsync(byte[] bytes, int count, string terminator, int timeoutMs, CancellationToken ct, Action ensureExecutionCurrent)
        => _io.RunBlockingAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            ensureExecutionCurrent();
            ValidateFrameOptions(count, terminator, timeoutMs);
            _channel.Write(bytes);
            return ReadFrame(count, terminator, timeoutMs, ct, ensureExecutionCurrent);
        }, timeoutMs == -1 || _channel.WriteTimeout == -1 ? -1 : Guard((int)Math.Min(int.MaxValue - 1000L, (long)timeoutMs + _channel.WriteTimeout)), "串口查询（写入至完整响应）", ct);

    private byte[] ReadFrame(int count, string terminatorText, int timeoutMs, CancellationToken ct, Action ensureExecutionCurrent)
    {
        ValidateFrameOptions(count, terminatorText, timeoutMs);
        var terminator = Encoding.UTF8.GetBytes((terminatorText ?? string.Empty).Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t"));
        if (count == 0 && terminator.Length == 0 && timeoutMs == -1)
            throw new ArgumentException("无限读取必须配置字节数或终止符");
        var clock = Stopwatch.StartNew();
        var temp = new byte[1024];
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            ensureExecutionCurrent();
            _lifetime.Token.ThrowIfCancellationRequested();
            var data = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer);
            int end = count > 0 ? (data.Length >= count ? count : -1) :
                terminator.Length > 0 ? data.IndexOf(terminator) is var index && index >= 0 ? index + terminator.Length : -1 : -1;
            if (end >= 0) return Take(end);
            int remaining = timeoutMs == -1 ? 100 : (int)Math.Max(0, timeoutMs - clock.ElapsedMilliseconds);
            if (remaining <= 0)
            {
                if (count == 0 && terminator.Length == 0 && _buffer.Count > 0) return Take(_buffer.Count);
                throw new TimeoutException($"串口读取超时({timeoutMs} ms)：已收到 {_buffer.Count} 字节，要求={(count > 0 ? count + " 字节" : "完整终止符")}");
            }
            // 小段阻塞式读取有自己的驱动期限，生命周期取消在段间检查；不能用 WaitAsync 假装原 Read 已取消。
            _channel.ReadTimeout = Math.Min(100, remaining);
            try
            {
                int read = _channel.Read(temp, 0, temp.Length);
                if (read < 0 || read > temp.Length) throw new IOException("串口驱动返回非法长度");
                if (_buffer.Count + read > MaxFrameBytes) throw new IOException("串口响应超过 16 MiB 上限");
                for (int i = 0; i < read; i++) _buffer.Add(temp[i]);
            }
            catch (TimeoutException) { /* 本段暂无数据，总期限及取消由循环检查。 */ }
        }
    }
    private static void ValidateFrameOptions(int count, string terminator, int timeoutMs)
    {
        if (count < 0 || count > MaxFrameBytes) throw new ArgumentOutOfRangeException(nameof(count));
        if (timeoutMs != -1 && timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (count == 0 && string.IsNullOrEmpty(terminator) && timeoutMs == -1) throw new ArgumentException("无限读取必须配置字节数或终止符");
    }
    private byte[] Take(int count) { var result = _buffer.GetRange(0, count).ToArray(); _buffer.RemoveRange(0, count); return result; }
    private static int Guard(int timeoutMs) => timeoutMs == -1 ? -1 : (int)Math.Min(int.MaxValue, (long)timeoutMs + 1000);
    public async Task CloseAsync(int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var shutdown = RequestShutdown();
        var closing = _io.CloseWithReleaseAsync(ReleaseAfterShutdownAsync, timeoutMs, ct);
        ObserveRelease();
        try { await closing.ConfigureAwait(false); }
        catch (Exception ex) when (shutdown.IsCompletedSuccessfully && shutdown.Result is { } stopError && !ct.IsCancellationRequested)
        { throw new AggregateException("串口关闭及中断请求失败", stopError, ex); }
        // The release callback already joined shutdown. A completed interrupt
        // failure is diagnostic once real I/O and final Dispose have succeeded.
    }
    public void Dispose()
    {
        var shutdown = RequestShutdown();
        _io.RequestAsyncClose(ReleaseAfterShutdownAsync);
        ObserveRelease();
        var actualClose = _io.CloseCompletion!;
        lock (_shutdownSync)
            if (_closeObserver is null || _closeObserver.IsCompleted)
                _closeObserver = Task.Run(() => ObserveCloseAsync(actualClose, shutdown));
    }
    private Task<Exception?> RequestShutdown()
    {
        Interlocked.Exchange(ref _closing, 1);
        _io.RequestStop();
        TrackRetirement();
        lock (_shutdownSync)
        {
            if (_shutdown is null || (!_releasing && !_releaseCompleted &&
                _shutdown.IsCompletedSuccessfully && _shutdown.Result is not null))
                _shutdown = Task.Run(ShutdownCoreAsync);
            return _shutdown;
        }
    }
    private async Task<Exception?> ShutdownCoreAsync()
    {
        List<Exception> errors = [];
        Task callbacks;
        try { callbacks = _lifetime.CancelAsync(); } catch (Exception ex) { callbacks = Task.FromException(ex); }
        var interrupt = _channel is IInterruptibleSerialChannel channel ? Task.Run(channel.CancelPendingIo) : Task.CompletedTask;
        try { await callbacks.ConfigureAwait(false); } catch (Exception ex) { errors.Add(ex); }
        try { await interrupt.ConfigureAwait(false); } catch (Exception ex) { errors.Add(ex); }
        return errors.Count switch { 0 => null, 1 => errors[0], _ => new AggregateException("串口中断请求失败", errors) };
    }
    private async Task ReleaseAfterShutdownAsync()
    {
        Task<Exception?> shutdown;
        lock (_shutdownSync) { _releasing = true; shutdown = _shutdown!; }
        try
        {
            var warning = await shutdown.ConfigureAwait(false);
            _channel.Dispose();
            CloseWarning = warning;
            // The shared gate publishes IsClosed after this callback returns.
            // Keep physical-release success permanent across that publication gap.
            lock (_shutdownSync) _releaseCompleted = true;
        }
        finally { lock (_shutdownSync) _releasing = false; }
    }
    private async Task ObserveCloseAsync(Task actualClose, Task<Exception?> shutdown)
    {
        Exception? failure = null;
        try { await actualClose.ConfigureAwait(false); } catch (Exception ex) { failure = ex; }
        if (await shutdown.ConfigureAwait(false) is { } stopError && failure != null)
            failure = new AggregateException("串口后台收尾失败", failure, stopError);
        if (failure is null)
        {
            if (CloseWarning is { } warning)
            {
                Trace.TraceWarning($"串口已实际释放，保留中断诊断：{warning}");
                try { _report?.Invoke(warning); }
                catch (Exception ex) { Trace.TraceError($"串口已释放后的诊断回调失败：{ex}"); }
            }
            return;
        }
        Trace.TraceError($"串口后台收尾诊断（已释放={IsClosed}）：{failure}");
        try { _report?.Invoke(failure); } catch (Exception ex) { Trace.TraceError($"串口收尾诊断回调失败：{ex}"); }
    }
    private void TrackRetirement()
    {
        if (PhysicalResourceKey != null && !_io.IsClosed)
            RetiringResources.Track(PhysicalResourceKey, this, CloseAsync, () => _io.IsClosed, () => _io.IsolatedReason ?? "串口正在关闭");
    }
    private void ObserveRelease()
    {
        if (PhysicalResourceKey != null && _io.CloseCompletion is { } actual)
            _ = actual.ContinueWith(_ => RetiringResources.ForgetReleased(PhysicalResourceKey, this), CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private sealed class SerialChannel(SysSerialPort port) : ISerialChannel, IInterruptibleSerialChannel
    {
        private readonly object _abortSync = new();
        public bool IsOpen => port.IsOpen;
        public int ReadTimeout { get => port.ReadTimeout; set => port.ReadTimeout = value; }
        public int WriteTimeout { get => port.WriteTimeout; set => port.WriteTimeout = value; }
        public int Read(byte[] buffer, int offset, int count) => port.Read(buffer, offset, count);
        public void Write(byte[] bytes) => port.Write(bytes, 0, bytes.Length);
        public void CancelPendingIo()
        {
            lock (_abortSync)
            {
                List<Exception> errors = [];
                bool open = true;
                try { open = port.IsOpen; } catch (Exception ex) { errors.Add(ex); }
                // .NET 8 Close flushes before disposing the stream. Purge first
                // requests RX/TXABORT so infinite writes can reach that close.
                if (open) { Attempt(port.DiscardOutBuffer); Attempt(port.DiscardInBuffer); }
                Attempt(port.Close);
                if (errors.Count != 0) throw new AggregateException("串口中断或关闭请求失败；已发送字节及远端结果未知", errors);
                void Attempt(Action action) { try { action(); } catch (Exception ex) { errors.Add(ex); } }
            }
        }
        public void Dispose() { lock (_abortSync) port.Dispose(); }
    }
}
