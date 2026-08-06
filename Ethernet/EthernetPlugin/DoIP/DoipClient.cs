using System.Net.Sockets;

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

    /// <summary>诊断仪源地址</summary>
    public ushort SourceAddress { get; }

    public DoipClient(TcpClient tcp, ushort sourceAddress, int timeoutMs)
    {
        _tcp = tcp;
        _stream = tcp.GetStream();
        SourceAddress = sourceAddress;
        _timeoutMs = timeoutMs;
    }

    /// <summary>发送路由激活请求（0x0005），等待响应（0x0006）并校验响应码 0x10。</summary>
    public async Task RoutingActivationAsync(byte activationType, CancellationToken ct)
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

        var responseCode = data[4];
        if (responseCode != 0x10)
            throw new InvalidOperationException($"路由激活被拒绝: 响应码 0x{responseCode:X2}");
    }

    /// <summary>发送诊断消息（0x8001）并等待诊断响应，返回 UDS 响应数据。</summary>
    public async Task<byte[]> DiagRequestAsync(ushort targetAddress, byte[] udsData, CancellationToken ct)
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
                    continue;
                case PtDiagNegativeAck:
                    var nack = data.Length >= 5 ? data[4] : (byte)0xFF;
                    throw new InvalidOperationException($"诊断消息被拒绝: NACK 码 0x{nack:X2}");
                case PtDiagMessage:
                    if (data.Length < 4)
                        throw new InvalidOperationException("诊断响应报文长度不足");
                    return data[4..];
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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeoutMs);

        var header = await ReadExactAsync(8, cts.Token);
        if (header[0] != ProtocolVersion || header[1] != unchecked((byte)~ProtocolVersion))
            throw new InvalidOperationException($"DoIP 协议版本不匹配: 0x{header[0]:X2}/0x{header[1]:X2}");

        var payloadType = (ushort)((header[2] << 8) | header[3]);
        var length = (header[4] << 24) | (header[5] << 16) | (header[6] << 8) | header[7];
        if (length < 0 || length > 0x400000)
            throw new InvalidOperationException($"DoIP 报文长度异常: {length}");

        var payload = await ReadExactAsync(length, cts.Token);
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

    public void Dispose()
    {
        _stream.Dispose();
        _tcp.Close();
        _tcp.Dispose();
    }
}
