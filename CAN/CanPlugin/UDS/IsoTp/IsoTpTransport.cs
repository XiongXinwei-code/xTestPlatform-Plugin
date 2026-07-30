using CAN.Adapters;
using CAN.Models;

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
    private readonly int _maxPayload; // 单帧最大有效载荷（Classic=7, FD=63）

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
        CanFrameType frameType = CanFrameType.Standard, bool useFd = false)
    {
        _adapter = adapter;
        _txId = txId;
        _rxId = rxId;
        _frameType = frameType;
        _useFd = useFd;
        _maxPayload = useFd ? 63 : 7; // FD 单帧 SF_DL 可达 62 字节（含 PCI），简化为 63
    }

    /// <summary>发送 UDS 请求数据（自动分段）</summary>
    public async Task SendAsync(byte[] data, CancellationToken ct = default)
    {
        if (data.Length <= _maxPayload)
        {
            SendSingleFrame(data);
        }
        else
        {
            await SendMultiFrameAsync(data, ct);
        }
    }

    /// <summary>接收 UDS 响应数据（自动重组）</summary>
    public async Task<byte[]?> ReceiveAsync(int timeoutMs, CancellationToken ct = default)
    {
        var msg = _adapter.Read(_rxId, timeoutMs, ct);
        if (msg == null || msg.Data.Length == 0) return null;

        byte pciType = (byte)(msg.Data[0] & 0xF0);

        return pciType switch
        {
            SingleFrame => ParseSingleFrame(msg.Data),
            FirstFrame => await ReceiveMultiFrameAsync(msg.Data, timeoutMs, ct),
            _ => null
        };
    }

    // ── 单帧 ────────────────────────────────────────────────────

    private void SendSingleFrame(byte[] data)
    {
        int frameLen = _useFd ? 8 : 8; // 最小帧长度填充到 8
        if (data.Length > 7 && _useFd)
        {
            // CAN FD 扩展单帧: PCI(00) + Length + Data
            var frame = new byte[Math.Max(data.Length + 2, 8)];
            frame[0] = 0x00; // SF with escape
            frame[1] = (byte)data.Length;
            Buffer.BlockCopy(data, 0, frame, 2, data.Length);
            WriteFrame(frame);
        }
        else
        {
            // 标准单帧: PCI(0X) + Data，X=数据长度
            var frame = new byte[8];
            frame[0] = (byte)(SingleFrame | (data.Length & 0x0F));
            Buffer.BlockCopy(data, 0, frame, 1, data.Length);
            WriteFrame(frame);
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
        var ff = new byte[8];
        ff[0] = (byte)(FirstFrame | ((totalLen >> 8) & 0x0F));
        ff[1] = (byte)(totalLen & 0xFF);
        int ffDataLen = 6; // 首帧数据区 = 8 - 2(PCI)
        Buffer.BlockCopy(data, 0, ff, 2, Math.Min(ffDataLen, data.Length));
        WriteFrame(ff);

        // 等待流控帧 (FC)
        var fc = _adapter.Read(_rxId, 1000, ct);
        if (fc == null || fc.Data.Length < 3 || (fc.Data[0] & 0xF0) != FlowControl)
            throw new InvalidOperationException("未收到流控帧");

        byte flowStatus = (byte)(fc.Data[0] & 0x0F);
        if (flowStatus != FlowStatus_CTS)
            throw new InvalidOperationException($"流控状态异常: {flowStatus}");

        int blockSize = fc.Data[1]; // 0 = 无限制
        int stMin = fc.Data[2];     // 最小间隔 (ms)

        // 发送连续帧 (CF)
        int offset = ffDataLen;
        byte seqNum = 1;
        int sentInBlock = 0;

        while (offset < totalLen && !ct.IsCancellationRequested)
        {
            var cf = new byte[8];
            cf[0] = (byte)(ConsecutiveFrame | (seqNum & 0x0F));
            int cfDataLen = Math.Min(7, totalLen - offset);
            Buffer.BlockCopy(data, offset, cf, 1, cfDataLen);
            WriteFrame(cf);

            offset += cfDataLen;
            seqNum = (byte)((seqNum + 1) & 0x0F);
            sentInBlock++;

            // 帧间隔
            if (stMin > 0)
                await Task.Delay(stMin, ct);

            // Block Size 控制
            if (blockSize > 0 && sentInBlock >= blockSize && offset < totalLen)
            {
                sentInBlock = 0;
                var nextFc = _adapter.Read(_rxId, 1000, ct);
                if (nextFc == null || (nextFc.Data[0] & 0xF0) != FlowControl)
                    throw new InvalidOperationException("未收到后续流控帧");
            }
        }
    }

    // ── 多帧接收 ────────────────────────────────────────────────

    private async Task<byte[]?> ReceiveMultiFrameAsync(byte[] ffData, int timeoutMs, CancellationToken ct)
    {
        // 解析首帧
        int totalLen = ((ffData[0] & 0x0F) << 8) | ffData[1];
        var buffer = new byte[totalLen];
        int ffDataLen = Math.Min(6, totalLen);
        Buffer.BlockCopy(ffData, 2, buffer, 0, ffDataLen);
        int received = ffDataLen;

        // 发送流控帧
        var fc = new byte[8];
        fc[0] = FlowControl | FlowStatus_CTS;
        fc[1] = 0x00; // BS = 0 (无限制)
        fc[2] = 0x0A; // STmin = 10ms
        WriteFrame(fc);

        // 接收连续帧
        byte expectedSeq = 1;
        while (received < totalLen && !ct.IsCancellationRequested)
        {
            var cf = _adapter.Read(_rxId, timeoutMs, ct);
            if (cf == null || cf.Data.Length == 0) return null;

            if ((cf.Data[0] & 0xF0) != ConsecutiveFrame) return null;

            byte seq = (byte)(cf.Data[0] & 0x0F);
            if (seq != expectedSeq) return null;

            int cfDataLen = Math.Min(7, totalLen - received);
            Buffer.BlockCopy(cf.Data, 1, buffer, received, cfDataLen);
            received += cfDataLen;
            expectedSeq = (byte)((expectedSeq + 1) & 0x0F);
        }

        return received >= totalLen ? buffer : null;
    }

    // ── 辅助 ────────────────────────────────────────────────────

    private void WriteFrame(byte[] data)
    {
        _adapter.Write(new CanMessage
        {
            Id = _txId,
            FrameType = _frameType,
            Data = data,
            IsFd = _useFd
        });
    }
}
