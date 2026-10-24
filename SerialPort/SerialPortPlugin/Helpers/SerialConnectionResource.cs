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

/// <summary>同一串口只有一个完整事务，余量帧归连接所有；取消不撤回已经发出的字节。</summary>
public sealed class SerialConnectionResource : IDisposable
{
    private const int MaxFrameBytes = 16 * 1024 * 1024;
    private readonly ISerialChannel _channel;
    private readonly PendingIoGate _io = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<byte> _buffer = new();
    private readonly Action<Exception>? _report;
    private volatile bool _ready;
    private int _closing;
    public string? PhysicalResourceKey { get; }
    public bool IsOpen => _ready && Volatile.Read(ref _closing) == 0 && _channel.IsOpen && !_io.IsClosed;
    public int WriteTimeout => _channel.WriteTimeout;
    public bool IsClosed => _io.IsClosed;
    internal bool IsClosing => Volatile.Read(ref _closing) != 0;

    internal void EnsureAvailableForPublication()
    {
        if (!_ready || Volatile.Read(ref _closing) != 0 || _io.IsClosed || _io.IsolatedReason != null)
            throw new InvalidOperationException("串口连接已关闭、正在收尾或被隔离，拒绝发布迟到结果");
    }

    public SerialConnectionResource(SysSerialPort port, Action<Exception>? report = null) : this(new SerialChannel(port), report, port.PortName) { _ready = false; }
    public SerialConnectionResource(ISerialChannel channel, Action<Exception>? report = null, string? physicalPort = null)
    { _channel = channel; _report = report; _ready = true; PhysicalResourceKey = physicalPort == null ? null : SerialPortHelper.GetPhysicalPortKey(physicalPort); }
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
    public Task CloseAsync(int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        Interlocked.Exchange(ref _closing, 1);
        TrackRetirement();
        var closing = _io.CloseAsync(_channel.Dispose, timeoutMs, ct);
        ObserveRelease();
        _lifetime.Cancel();
        return closing;
    }
    public void Dispose() { Interlocked.Exchange(ref _closing, 1); TrackRetirement(); _io.RequestClose(_channel.Dispose, _report); ObserveRelease(); _lifetime.Cancel(); }
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

    private sealed class SerialChannel(SysSerialPort port) : ISerialChannel
    {
        public bool IsOpen => port.IsOpen;
        public int ReadTimeout { get => port.ReadTimeout; set => port.ReadTimeout = value; }
        public int WriteTimeout { get => port.WriteTimeout; set => port.WriteTimeout = value; }
        public int Read(byte[] buffer, int offset, int count) => port.Read(buffer, offset, count);
        public void Write(byte[] bytes) => port.Write(bytes, 0, bytes.Length);
        public void Dispose() => port.Dispose();
    }
}
