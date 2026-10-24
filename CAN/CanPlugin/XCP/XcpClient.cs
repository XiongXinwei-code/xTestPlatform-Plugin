using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;

namespace CAN.XCP;

/// <summary>XCP CONNECT 响应信息</summary>
public class XcpConnectResponse
{
    /// <summary>资源掩码（标定/测量/编程等权限位）</summary>
    public byte ResourceMask { get; set; }
    /// <summary>通信模式（字节序等）</summary>
    public byte CommMode { get; set; }
    /// <summary>最大 CTO 长度（命令传输对象）</summary>
    public byte MaxCto { get; set; }
    /// <summary>最大 DTO 长度（数据传输对象）</summary>
    public ushort MaxDto { get; set; }
    /// <summary>XCP 协议版本</summary>
    public byte ProtocolVersion { get; set; }
    /// <summary>XCP 传输层版本</summary>
    public byte TransportVersion { get; set; }
    /// <summary>是否支持标定（CAL/PAG）</summary>
    public bool SupportsCalibration => (ResourceMask & 0x01) != 0;
    /// <summary>是否支持数据采集（DAQ）</summary>
    public bool SupportsDaq => (ResourceMask & 0x04) != 0;
    /// <summary>是否支持编程（PGM）</summary>
    public bool SupportsProgramming => (ResourceMask & 0x10) != 0;
    /// <summary>从站字节序</summary>
    public bool IsBigEndian => (CommMode & 0x01) != 0;
}

/// <summary>
/// XCP on CAN 客户端，封装 CONNECT / DISCONNECT / SHORT_UPLOAD / SHORT_DOWNLOAD 命令。
/// 需配合已打开的 ICanAdapter 使用。
/// </summary>
public class XcpClient
{
    private readonly ICanAdapter _adapter;
    private readonly uint _txId;
    private readonly uint _rxId;
    private readonly int _timeoutMs;

    public XcpClient(ICanAdapter adapter, uint txId, uint rxId, int timeoutMs = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        if (adapter is CAN.Runtime.CanConnectionResource resource) resource.ClaimRawProtocol();
        _adapter = adapter;
        _txId    = txId;
        _rxId    = rxId;
        _timeoutMs = timeoutMs;
    }

    /// <summary>发送 CONNECT 命令（0xFF）并解析从站能力</summary>
    public Task<XcpConnectResponse> ConnectAsync(XcpConnectMode mode = XcpConnectMode.Normal, CancellationToken ct = default) =>
        CanProtocolTransaction.RunAsync(_adapter, _timeoutMs, token => ConnectCoreAsync(mode, token), ct);

    private async Task<XcpConnectResponse> ConnectCoreAsync(XcpConnectMode mode, CancellationToken ct)
    {
        var response = await SendReceiveAsync([(byte)0xFF, (byte)mode], ct);
        if (response[0] != 0xFF)
            ThrowNegative(response, "CONNECT");
        if (response.Length < 8) throw new InvalidOperationException("XCP CONNECT 响应长度不足 8 字节");

        return new XcpConnectResponse
        {
            ResourceMask      = response[1],
            CommMode          = response[2],
            MaxCto            = response[3],
            MaxDto            = BitConverter.ToUInt16(response, 4),
            ProtocolVersion   = response[6],
            TransportVersion  = response[7]
        };
    }

    /// <summary>发送 DISCONNECT 命令（0xFE）</summary>
    public async Task DisconnectAsync(CancellationToken ct = default) =>
        await CanProtocolTransaction.RunAsync(_adapter, _timeoutMs, async token => { await DisconnectCoreAsync(token).ConfigureAwait(false); return true; }, ct).ConfigureAwait(false);

    private async Task DisconnectCoreAsync(CancellationToken ct)
    {
        var response = await SendReceiveAsync([0xFE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], ct);
        if (response[0] != 0xFF)
            ThrowNegative(response, "DISCONNECT");
    }

    /// <summary>SHORT_UPLOAD（0xF4）：从 ECU 地址读取最多 7 字节</summary>
    public Task<byte[]> ShortUploadAsync(uint address, byte addrExt, byte length, CancellationToken ct = default) =>
        CanProtocolTransaction.RunAsync(_adapter, _timeoutMs, token => ShortUploadCoreAsync(address, addrExt, length, token), ct);

    private async Task<byte[]> ShortUploadCoreAsync(uint address, byte addrExt, byte length, CancellationToken ct)
    {
        if (length is 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(length), "SHORT_UPLOAD 长度必须在 1-7 之间");

        var addrBytes = BitConverter.GetBytes(address);
        byte[] cmd = [0xF4, length, 0x00, addrExt, addrBytes[0], addrBytes[1], addrBytes[2], addrBytes[3]];

        var response = await SendReceiveAsync(cmd, ct);
        if (response[0] != 0xFF)
            ThrowNegative(response, "SHORT_UPLOAD");
        if (response.Length < length + 1)
            throw new InvalidOperationException($"XCP SHORT_UPLOAD 正应答长度不足：请求 {length} 字节，实际数据 {response.Length - 1} 字节");

        // 有效数据从字节1开始
        return response.Skip(1).Take(length).ToArray();
    }

    /// <summary>
    /// 向 ECU 地址写入最多 6 字节。
    /// 经典 CAN 的 MAX_CTO 为 8，单帧 SHORT_DOWNLOAD（0xED，需 8+n 字节）无法承载，
    /// 因此按协议使用 SET_MTA（0xF6）设置地址，再以 DOWNLOAD（0xF0）携带数据写入。
    /// </summary>
    public async Task ShortDownloadAsync(uint address, byte addrExt, byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var copy = data.ToArray();
        await CanProtocolTransaction.RunAsync(_adapter, _timeoutMs, async token =>
        { await ShortDownloadCoreAsync(address, addrExt, copy, token).ConfigureAwait(false); return true; }, ct).ConfigureAwait(false);
    }

    private async Task ShortDownloadCoreAsync(uint address, byte addrExt, byte[] data, CancellationToken ct)
    {
        if (data.Length is 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(data), "SHORT_DOWNLOAD 数据必须在 1-6 字节之间");

        var addrBytes = BitConverter.GetBytes(address);
        byte[] setMta = [0xF6, 0x00, 0x00, addrExt, addrBytes[0], addrBytes[1], addrBytes[2], addrBytes[3]];

        var mtaResponse = await SendReceiveAsync(setMta, ct);
        if (mtaResponse[0] != 0xFF)
            ThrowNegative(mtaResponse, "SET_MTA");

        var download = new byte[2 + data.Length];
        download[0] = 0xF0;
        download[1] = (byte)data.Length;
        data.CopyTo(download, 2);

        var response = await SendReceiveAsync(download, ct);
        if (response[0] != 0xFF)
            ThrowNegative(response, "DOWNLOAD");
    }

    // ────────────────────────────────────────────────────────────────
    // 内部方法
    // ────────────────────────────────────────────────────────────────

    private async Task<byte[]> SendReceiveAsync(byte[] payload, CancellationToken ct)
    {
        var msg = new CanMessage
        {
            Id        = _txId,
            Data      = PadTo8(payload),
            FrameType = CAN.Models.CanFrameType.Standard
        };
        await CanRawIo.WriteAsync(_adapter, msg, _timeoutMs, ct).ConfigureAwait(false);
        return await WaitResponseAsync(ct);
    }

    private async Task<byte[]> WaitResponseAsync(CancellationToken ct)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < _timeoutMs)
        {
            ct.ThrowIfCancellationRequested();
            var msg = await CanRawIo.ReadAsync(_adapter, _rxId, Math.Max(1, _timeoutMs - (int)clock.ElapsedMilliseconds), ct).ConfigureAwait(false);
            if (msg != null && msg.Data.Length > 0)
                return msg.Data;
            await Task.Delay(1, ct);
        }
        throw new TimeoutException($"XCP 响应超时（TxId=0x{_txId:X}, RxId=0x{_rxId:X}, Timeout={_timeoutMs}ms）");
    }

    private static byte[] PadTo8(byte[] src)
    {
        if (src.Length >= 8) return src[..8];
        var result = new byte[8];
        src.CopyTo(result, 0);
        return result;
    }

    private static void ThrowNegative(byte[] response, string cmd)
    {
        byte errCode = response.Length > 1 ? response[1] : (byte)0;
        string desc = errCode switch
        {
            0x00 => "CMD_OK（不应出现在否定响应中）",
            0x10 => "CMD_SYNCH",
            0x20 => "CMD_BUSY",
            0x21 => "DAQ_ACTIVE",
            0x22 => "PRM_ACTIVE",
            0x30 => "CMD_UNKNOWN",
            0x31 => "CMD_SYNTAX",
            0x32 => "OUT_OF_RANGE",
            0x33 => "WRITE_PROTECTED",
            0x34 => "ACCESS_DENIED",
            0x35 => "ACCESS_LOCKED",
            0x36 => "PAGE_NOT_VALID",
            0x37 => "MODE_NOT_VALID",
            0x38 => "SEGMENT_NOT_VALID",
            0x39 => "SEQUENCE",
            0x3A => "DAQ_CONFIG",
            0x40 => "MEMORY_OVERFLOW",
            0x41 => "GENERIC",
            0x42 => "VERIFY",
            _    => $"UNKNOWN(0x{errCode:X2})"
        };
        throw new InvalidOperationException($"XCP {cmd} 否定响应: {desc} (0x{errCode:X2})");
    }
}
