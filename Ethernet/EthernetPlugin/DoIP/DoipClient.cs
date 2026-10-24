using System.Net.Sockets;
using System.Diagnostics;

namespace Ethernet.DoIP;

/// <summary>DoIP（ISO 13400-2）协议客户端：报文封装/解析、路由激活、诊断消息收发。</summary>
public sealed class DoipClient : IDisposable
{
    private const byte ProtocolVersion = 0x02;

    // Payload Types
    private const ushort PtRoutingActivationRequest  = 0x0005;
    private const ushort PtRoutingActivationResponse = 0x0006;
    private const ushort PtDiagMessage               = 0x8001;
    private const ushort PtDiagPositiveAck           = 0x8002;
    private const ushort PtDiagNegativeAck           = 0x8003;

    private readonly TcpClient _tcp;
    private readonly NetworkStream _stream;
    private readonly int _timeoutMs;
    private readonly SemaphoreSlim _transaction = new(1, 1);
    private int _disposed;
    private volatile string? _fault;
    public bool IsUsable => Volatile.Read(ref _disposed) == 0 && _fault == null;

    /// <summary>诊断仪源地址</summary>
    public ushort SourceAddress { get; }

    public DoipClient(TcpClient tcp, ushort sourceAddress, int timeoutMs)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        _tcp = tcp;
        _stream = tcp.GetStream();
        SourceAddress = sourceAddress;
        _timeoutMs = timeoutMs;
    }

    /// <summary>发送路由激活请求（0x0005），等待响应（0x0006）并校验响应码 0x10。</summary>
    public Task RoutingActivationAsync(byte activationType, CancellationToken ct) =>
        RunTransactionAsync(async token => { await RoutingActivationCoreAsync(activationType, token).ConfigureAwait(false); return true; }, _timeoutMs, ct);

    private async Task RoutingActivationCoreAsync(byte activationType, CancellationToken ct)
    {
        var payload = new byte[7];
        payload[0] = (byte)(SourceAddress >> 8);
        payload[1] = (byte)SourceAddress;
        payload[2] = activationType;
        // 4 字节保留 = 0x00000000

        await SendAsync(PtRoutingActivationRequest, payload, ct);

        var (type, data) = await ReceiveAsync(ct);
        if (type != PtRoutingActivationResponse)
            throw new InvalidOperationException($"路由激活失败: 收到意外的 PayloadType 0x{type:X4}");
        if (data.Length < 9)
            throw new InvalidOperationException("路由激活失败: 响应报文长度不足");
        if (((data[0] << 8) | data[1]) != SourceAddress)
            throw new InvalidDataException("DoIP 路由激活响应的诊断仪地址不匹配");

        var responseCode = data[4];
        if (responseCode != 0x10)
            throw new InvalidOperationException($"路由激活被拒绝: 响应码 0x{responseCode:X2}");
    }

    /// <summary>发送诊断消息（0x8001）并等待诊断响应，返回 UDS 响应数据。</summary>
    public Task<byte[]> DiagRequestAsync(ushort targetAddress, byte[] udsData, CancellationToken ct)
        => DiagRequestAsync(targetAddress, udsData, _timeoutMs, ct);

    /// <summary>本次请求预算包含事务排队、提交、所有 ACK/待定响应及完整诊断响应。</summary>
    public Task<byte[]> DiagRequestAsync(ushort targetAddress, byte[] udsData, int timeoutMs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(udsData);
        if (udsData.Length == 0) throw new ArgumentException("UDS 请求不能为空", nameof(udsData));
        return RunTransactionAsync(token => DiagRequestCoreAsync(targetAddress, udsData, token), timeoutMs, ct);
    }

    private async Task<byte[]> DiagRequestCoreAsync(ushort targetAddress, byte[] udsData, CancellationToken ct)
    {
        var payload = new byte[4 + udsData.Length];
        payload[0] = (byte)(SourceAddress >> 8);
        payload[1] = (byte)SourceAddress;
        payload[2] = (byte)(targetAddress >> 8);
        payload[3] = (byte)targetAddress;
        udsData.CopyTo(payload, 4);

        await SendAsync(PtDiagMessage, payload, ct);

        // 循环接收：跳过 ACK（0x8002），等待诊断响应（0x8001）
        while (true)
        {
            var (type, data) = await ReceiveAsync(ct);
            switch (type)
            {
                case PtDiagPositiveAck:
                    // Gateways may ACK for a downstream ECU; only the tester address is fixed here.
                    if (data.Length < 5 || ((data[2] << 8) | data[3]) != SourceAddress || data[4] != 0)
                        throw new InvalidDataException("DoIP ACK 长度、目标地址或状态无效");
                    continue;
                case PtDiagNegativeAck:
                    var nack = data.Length >= 5 ? data[4] : (byte)0xFF;
                    throw new InvalidOperationException($"诊断消息被拒绝: NACK 码 0x{nack:X2}");
                case PtDiagMessage:
                    if (data.Length < 5)
                        throw new InvalidDataException("DoIP 诊断响应报文长度不足");
                    if (((data[0] << 8) | data[1]) != targetAddress || ((data[2] << 8) | data[3]) != SourceAddress)
                        throw new InvalidDataException("DoIP 诊断响应源/目标逻辑地址不匹配");
                    var uds = data[4..];
                    if (uds[0] == 0x7F)
                    {
                        if (uds.Length < 3 || uds[1] != udsData[0])
                            throw new InvalidDataException("DoIP UDS 否定响应长度或服务 ID 不匹配");
                        if (uds[2] == 0x78) continue;
                    }
                    else if (uds[0] != unchecked((byte)(udsData[0] + 0x40)))
                        throw new InvalidDataException("DoIP UDS 正响应服务 ID 不匹配");
                    return uds;
                default:
                    throw new InvalidOperationException($"收到意外的 PayloadType 0x{type:X4}");
            }
        }
    }

    private async Task SendAsync(ushort payloadType, byte[] payload, CancellationToken ct)
    {
        var frame = new byte[8 + payload.Length];
        frame[0] = ProtocolVersion;
        frame[1] = unchecked((byte)~ProtocolVersion);
        frame[2] = (byte)(payloadType >> 8);
        frame[3] = (byte)payloadType;
        frame[4] = (byte)(payload.Length >> 24);
        frame[5] = (byte)(payload.Length >> 16);
        frame[6] = (byte)(payload.Length >> 8);
        frame[7] = (byte)payload.Length;
        payload.CopyTo(frame, 8);

        await _stream.WriteAsync(frame, ct);
    }

    private async Task<(ushort payloadType, byte[] payload)> ReceiveAsync(CancellationToken ct)
    {
        var header = await ReadExactAsync(8, ct);
        if (header[0] != ProtocolVersion || header[1] != unchecked((byte)~ProtocolVersion))
            throw new InvalidOperationException($"DoIP 协议版本不匹配: 0x{header[0]:X2}/0x{header[1]:X2}");

        var payloadType = (ushort)((header[2] << 8) | header[3]);
        var length = (header[4] << 24) | (header[5] << 16) | (header[6] << 8) | header[7];
        if (length < 0 || length > 0x400000)
            throw new InvalidOperationException($"DoIP 报文长度异常: {length}");

        var payload = await ReadExactAsync(length, ct);
        return (payloadType, payload);
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await _stream.ReadAsync(buffer.AsMemory(read, count - read), ct);
            if (n == 0)
                throw new InvalidOperationException("DoIP 连接已被远端关闭");
            read += n;
        }
        return buffer;
    }

    private async Task<T> RunTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "DoIP 请求总超时必须大于 0");
        ct.ThrowIfCancellationRequested();
        CheckUsable();
        var startedAt = Stopwatch.GetTimestamp();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeoutMs);
        bool entered = false, operationStarted = false;
        string TimeoutMessage() => operationStarted
            ? $"DoIP 事务超过 {timeoutMs} ms（含排队、ACK 和完整响应）；请求可能已发送，远端执行结果未知；本次会话已关闭，请由序列决定重新连接"
            : $"DoIP 事务排队或提交前等待超过 {timeoutMs} ms；本次未发送请求，会话未因本次等待关闭";
        void CheckDeadline()
        {
            ct.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds >= timeoutMs || deadline.IsCancellationRequested)
                throw new TimeoutException(TimeoutMessage());
        }
        try
        {
            await _transaction.WaitAsync(deadline.Token).ConfigureAwait(false);
            entered = true;
            CheckUsable();
            CheckDeadline();
            operationStarted = true;
            var result = await operation(deadline.Token).ConfigureAwait(false);
            CheckDeadline();
            return result;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            if (operationStarted) Abort("DoIP 事务超时，旧响应或部分帧状态不可复用");
            throw new TimeoutException(TimeoutMessage(), ex);
        }
        catch
        {
            if (operationStarted) Abort("DoIP 事务取消或失败，执行结果未知，请由序列决定重新连接");
            throw;
        }
        finally { if (entered) _transaction.Release(); }
    }

    private void CheckUsable()
    {
        if (!IsUsable) throw new InvalidOperationException(_fault ?? "DoIP 会话已关闭，请重新连接");
    }

    private void Abort(string reason) { _fault ??= reason; Dispose(); }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _tcp.Dispose();
        // The in-flight async transaction still owns its semaphore release.
    }
}
