using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;

namespace CAN.UDS.IsoTp;

/// <summary>
/// ISO 15765-2 (ISO-TP) 传输层实现。
/// 负责将超过单帧容量的 UDS 数据进行分段发送和重组接收。
/// </summary>
public sealed class IsoTpTransport
{
    private readonly ICanAdapter _adapter;
    private readonly uint _txId;
    private readonly uint _rxId;
    private readonly CanFrameType _frameType;
    private readonly bool _useFd;
    private readonly int _frameDataLength; // CAN 数据区长度（Classic=8, FD=64）
    private readonly int _maxSingleFramePayload; // 单帧最大 UDS 载荷（Classic=7, FD=62）
    public const int DefaultMaxReceivePayloadBytes = 4 * 1024 * 1024;
    private readonly int _maxReceivePayloadBytes;
    private readonly int _ioTimeoutMs;

    // ISO-TP 帧类型 (高 4 位)
    private const byte SingleFrame = 0x00;
    private const byte FirstFrame = 0x10;
    private const byte ConsecutiveFrame = 0x20;
    private const byte FlowControl = 0x30;

    // 流控参数
    private const byte FlowStatus_CTS = 0x00;       // Continue To Send
    private const byte FlowStatus_Wait = 0x01;      // Wait
    private const byte FlowStatus_Overflow = 0x02;  // Overflow

    public IsoTpTransport(ICanAdapter adapter, uint txId, uint rxId,
        CanFrameType frameType = CanFrameType.Standard, bool useFd = false,
        int maxReceivePayloadBytes = DefaultMaxReceivePayloadBytes, int ioTimeoutMs = 5000)
    {
        if (maxReceivePayloadBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxReceivePayloadBytes));
        _maxReceivePayloadBytes = maxReceivePayloadBytes;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ioTimeoutMs);
        _ioTimeoutMs = ioTimeoutMs;
        if (adapter is CAN.Runtime.CanConnectionResource resource) resource.ClaimRawProtocol();
        _adapter = adapter;
        _txId = txId;
        _rxId = rxId;
        _frameType = frameType;
        _useFd = useFd;
        _frameDataLength = useFd ? 64 : 8;
        // CAN FD 的扩展单帧需要 00 + SF_DL 两个 PCI 字节，因此最大载荷是 62 字节。
        _maxSingleFramePayload = useFd ? 62 : 7;
    }

    /// <summary>发送 UDS 请求数据（自动分段）</summary>
    public async Task SendAsync(byte[] data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (data.Length == 0) throw new ArgumentException("ISO-TP 载荷不能为空", nameof(data));
        if (data.Length <= _maxSingleFramePayload)
        {
            await SendSingleFrameAsync(data, ct).ConfigureAwait(false);
        }
        else
        {
            await SendMultiFrameAsync(data, ct);
        }
    }

    /// <summary>接收 UDS 响应数据（自动重组）</summary>
    public async Task<byte[]?> ReceiveAsync(int timeoutMs, CancellationToken ct = default)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        while (!ct.IsCancellationRequested)
        {
            int remaining = timeoutMs - (int)clock.ElapsedMilliseconds;
            if (remaining <= 0) return null;

            var msg = await CanRawIo.ReadAsync(_adapter, _rxId, remaining, ct).ConfigureAwait(false);
            if (msg == null || msg.Data.Length == 0) return null;

            byte pciType = (byte)(msg.Data[0] & 0xF0);

            // 队列中可能残留流控帧、连续帧或其他非诊断帧，忽略后继续等待，
            // 不能直接返回 null，否则会被上层误判为接收超时。
            if (pciType == SingleFrame)
            {
                var single = ParseSingleFrame(msg.Data);
                if (single?.Length > _maxReceivePayloadBytes) throw new InvalidOperationException("ISO-TP 单帧响应超过配置上限");
                return single;
            }
            if (pciType == FirstFrame)
            {
                remaining = timeoutMs - (int)clock.ElapsedMilliseconds;
                if (remaining <= 0) return null;
                return await ReceiveMultiFrameAsync(msg.Data, remaining, ct);
            }
        }

        return null;
    }

    // ── 单帧 ────────────────────────────────────────────────────

    private async Task SendSingleFrameAsync(byte[] data, CancellationToken ct)
    {
        if (data.Length > 7 && _useFd)
        {
            // CAN FD 扩展单帧: PCI(00) + Length + Data
            var frame = new byte[Math.Max(data.Length + 2, 8)];
            frame[0] = 0x00; // SF with escape
            frame[1] = (byte)data.Length;
            Buffer.BlockCopy(data, 0, frame, 2, data.Length);
            await WriteFrameAsync(frame, ct).ConfigureAwait(false);
        }
        else
        {
            // 标准单帧: PCI(0X) + Data，X=数据长度
            var frame = new byte[8];
            frame[0] = (byte)(SingleFrame | (data.Length & 0x0F));
            Buffer.BlockCopy(data, 0, frame, 1, data.Length);
            await WriteFrameAsync(frame, ct).ConfigureAwait(false);
        }
    }

    private static byte[]? ParseSingleFrame(byte[] frameData)
    {
        int len = frameData[0] & 0x0F;
        if (len == 0 && frameData.Length > 1)
        {
            // CAN FD 扩展单帧
            len = frameData[1];
            if (len + 2 > frameData.Length) return null;
            var result = new byte[len];
            Buffer.BlockCopy(frameData, 2, result, 0, len);
            return result;
        }

        if (len == 0 || len + 1 > frameData.Length) return null;
        var data = new byte[len];
        Buffer.BlockCopy(frameData, 1, data, 0, len);
        return data;
    }

    // ── 多帧发送 ────────────────────────────────────────────────

    private async Task SendMultiFrameAsync(byte[] data, CancellationToken ct)
    {
        // 发送首帧 (FF)
        int totalLen = data.Length;
        var ff = new byte[_frameDataLength];
        int ffPciLength;
        if (totalLen <= 0x0FFF)
        {
            ff[0] = (byte)(FirstFrame | ((totalLen >> 8) & 0x0F));
            ff[1] = (byte)(totalLen & 0xFF);
            ffPciLength = 2;
        }
        else
        {
            // ISO 15765-2:2016 扩展首帧长度：10 00 + 32 位消息长度。
            ff[0] = FirstFrame;
            ff[1] = 0x00;
            ff[2] = (byte)(totalLen >> 24);
            ff[3] = (byte)(totalLen >> 16);
            ff[4] = (byte)(totalLen >> 8);
            ff[5] = (byte)totalLen;
            ffPciLength = 6;
        }

        int ffDataLen = Math.Min(_frameDataLength - ffPciLength, data.Length);
        Buffer.BlockCopy(data, 0, ff, ffPciLength, ffDataLen);
        await WriteFrameAsync(ff, ct).ConfigureAwait(false);

        // 等待流控帧 (FC)
        var (blockSize, stMin) = await ReadFlowControlAsync(ct).ConfigureAwait(false);

        // 发送连续帧 (CF)
        int offset = ffDataLen;
        byte seqNum = 1;
        int sentInBlock = 0;

        while (offset < totalLen)
        {
            ct.ThrowIfCancellationRequested();
            int cfDataLen = Math.Min(_frameDataLength - 1, totalLen - offset);
            // 对齐 ZLG 的“短帧填充”行为：中间帧使用完整 DLC，末帧只取容纳有效
            // 数据所需的最小长度（适配器会向上对齐到合法 CAN FD DLC）。
            int cfFrameLength = _useFd ? Math.Max(cfDataLen + 1, 8) : 8;
            var cf = new byte[cfFrameLength];
            cf[0] = (byte)(ConsecutiveFrame | (seqNum & 0x0F));
            Buffer.BlockCopy(data, offset, cf, 1, cfDataLen);
            await WriteFrameAsync(cf, ct).ConfigureAwait(false);

            offset += cfDataLen;
            seqNum = (byte)((seqNum + 1) & 0x0F);
            sentInBlock++;

            // 帧间隔
            await DelayForStMinAsync(stMin, ct);

            // Block Size 控制
            if (blockSize > 0 && sentInBlock >= blockSize && offset < totalLen)
            {
                sentInBlock = 0;
                (blockSize, stMin) = await ReadFlowControlAsync(ct).ConfigureAwait(false);
            }
        }
    }

    private async Task<(int BlockSize, int StMin)> ReadFlowControlAsync(CancellationToken ct)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int waits = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            int remaining = _ioTimeoutMs - (int)clock.ElapsedMilliseconds;
            if (remaining <= 0) throw new TimeoutException("等待 ISO-TP 流控超时");
            var fc = await CanRawIo.ReadAsync(_adapter, _rxId, remaining, ct).ConfigureAwait(false);
            if (fc == null) throw new TimeoutException("等待 ISO-TP 流控超时");
            if (fc.Data.Length < 3 || (fc.Data[0] & 0xF0) != FlowControl)
                throw new InvalidOperationException("ISO-TP 流控帧长度或类型非法");
            switch (fc.Data[0] & 0x0F)
            {
                case FlowStatus_Wait:
                    if (++waits > 16) throw new TimeoutException("ISO-TP WAIT 次数超过 16 次");
                    continue;
                case FlowStatus_Overflow:
                    throw new InvalidOperationException("ECU 返回 ISO-TP Overflow，已停止发送");
                case FlowStatus_CTS:
                    if (fc.Data[2] > 0x7F && fc.Data[2] is not (>= 0xF1 and <= 0xF9))
                        throw new InvalidOperationException("ISO-TP STmin 使用保留值");
                    return (fc.Data[1], fc.Data[2]);
                default:
                    throw new InvalidOperationException("ISO-TP 流控状态非法");
            }
        }
    }

    // ── 多帧接收 ────────────────────────────────────────────────

    private async Task<byte[]?> ReceiveMultiFrameAsync(byte[] ffData, int timeoutMs, CancellationToken ct)
    {
        var receiveClock = System.Diagnostics.Stopwatch.StartNew();
        // 解析首帧
        if (ffData.Length < 2)
            return null;

        int totalLen = ((ffData[0] & 0x0F) << 8) | ffData[1];
        int ffPciLength = 2;
        if (totalLen == 0)
        {
            if (ffData.Length < 6)
                return null;

            uint extendedLength = ((uint)ffData[2] << 24) |
                                  ((uint)ffData[3] << 16) |
                                  ((uint)ffData[4] << 8) |
                                  ffData[5];
            if (extendedLength == 0 || extendedLength > int.MaxValue)
                return null;

            totalLen = (int)extendedLength;
            ffPciLength = 6;
        }

        if (totalLen > _maxReceivePayloadBytes)
        {
            await WriteFrameAsync([FlowControl | FlowStatus_Overflow, 0, 0, 0, 0, 0, 0, 0], ct).ConfigureAwait(false);
            throw new InvalidOperationException($"ISO-TP 声明长度 {totalLen} 超过接收上限 {_maxReceivePayloadBytes} 字节");
        }
        var buffer = new byte[totalLen];
        int ffDataLen = Math.Min(ffData.Length - ffPciLength, totalLen);
        Buffer.BlockCopy(ffData, ffPciLength, buffer, 0, ffDataLen);
        int received = ffDataLen;

        // 发送流控帧
        var fc = new byte[8];
        fc[0] = FlowControl | FlowStatus_CTS;
        fc[1] = 0x00; // BS = 0 (无限制)
        fc[2] = 0x0A; // STmin = 10ms
        await WriteFrameAsync(fc, ct).ConfigureAwait(false);

        // 接收连续帧
        byte expectedSeq = 1;
        while (received < totalLen && !ct.IsCancellationRequested)
        {
            int remaining = timeoutMs - (int)receiveClock.ElapsedMilliseconds;
            if (remaining <= 0) return null;
            var cf = await CanRawIo.ReadAsync(_adapter, _rxId, remaining, ct).ConfigureAwait(false);
            if (cf == null || cf.Data.Length == 0) return null;

            if ((cf.Data[0] & 0xF0) != ConsecutiveFrame) return null;

            byte seq = (byte)(cf.Data[0] & 0x0F);
            if (seq != expectedSeq) return null;

            int cfDataLen = Math.Min(cf.Data.Length - 1, totalLen - received);
            if (cfDataLen <= 0) return null;
            Buffer.BlockCopy(cf.Data, 1, buffer, received, cfDataLen);
            received += cfDataLen;
            expectedSeq = (byte)((expectedSeq + 1) & 0x0F);
        }

        return received >= totalLen ? buffer : null;
    }

    // ── 辅助 ────────────────────────────────────────────────────

    private static async Task DelayForStMinAsync(int stMin, CancellationToken ct)
    {
        if (stMin <= 0x7F)
        {
            if (stMin > 0)
                await Task.Delay(stMin, ct);
            return;
        }

        if (stMin is >= 0xF1 and <= 0xF9)
        {
            // 0xF1~0xF9 分别表示 100~900 微秒，不能直接当成 241~249 ms。
            double microseconds = (stMin - 0xF0) * 100d;
            await Task.Delay(TimeSpan.FromTicks((long)(microseconds * 10)), ct);
        }
    }

    private Task WriteFrameAsync(byte[] data, CancellationToken ct)
    {
        return CanRawIo.WriteAsync(_adapter, new CanMessage
        {
            Id = _txId,
            FrameType = _frameType,
            Data = data,
            IsFd = _useFd
        }, _ioTimeoutMs, ct);
    }
}
