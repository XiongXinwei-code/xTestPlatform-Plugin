using System.Buffers.Binary;
using CAN.Models;

namespace CAN.Adapters.NiXnet;

/// <summary>NI-XNET CAN 适配器实现</summary>
public sealed class NiXnetAdapter : ICanAdapter
{
    private uint _rxSession;
    private uint _txSession;
    private bool _isConnected;
    private CanProtocolType _protocol;
    private readonly object _lock = new();
    private readonly Queue<CanMessage> _pendingFrames = new();

    public bool IsConnected => _isConnected;

    public void Open(CanAdapterConfig config)
    {
        if (_isConnected) throw new InvalidOperationException("CAN 通道已打开");

        try
        {
            OpenInternal(config);
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "未找到 nixnet.dll，请安装 NI-XNET 驱动程序。" +
                "下载地址: https://www.ni.com/zh-cn/support/downloads/drivers/download.ni-xnet.html");
        }
        catch
        {
            // 中途失败时清理已创建的会话，避免接口被占用
            if (_rxSession != 0) { NiXnetApi.nxClear(_rxSession); _rxSession = 0; }
            if (_txSession != 0) { NiXnetApi.nxClear(_txSession); _txSession = 0; }
            throw;
        }
    }

    private void OpenInternal(CanAdapterConfig config)
    {
        _protocol = config.Protocol;
        var interfaceName = config.Channel; // 如 "CAN1"

        // 创建接收会话（Frame In Stream，使用 :memory: 内存数据库，无需 FIBEX/DBC 数据库文件）
        var status = NiXnetApi.nxCreateSession(
            NiXnetApi.InMemoryDatabase, "", "", interfaceName,
            NiXnetApi.nxMode_FrameInStream,
            out _rxSession);
        NiXnetApi.CheckStatus(status);

        // 创建发送会话（Frame Out Stream）
        status = NiXnetApi.nxCreateSession(
            NiXnetApi.InMemoryDatabase, "", "", interfaceName,
            NiXnetApi.nxMode_FrameOutStream,
            out _txSession);
        NiXnetApi.CheckStatus(status);

        // 设置波特率
        uint baudRate = (uint)config.BaudRate;
        status = NiXnetApi.nxSetProperty(_rxSession,
            NiXnetApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
        NiXnetApi.CheckStatus(status);

        status = NiXnetApi.nxSetProperty(_txSession,
            NiXnetApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
        NiXnetApi.CheckStatus(status);

        // 设置 CAN IO 模式
        uint ioMode = config.Protocol switch
        {
            CanProtocolType.FD => NiXnetApi.nxCANioMode_CAN_FD_BRS,
            CanProtocolType.XL => throw new NotSupportedException("NI-XNET 不支持 CAN XL 协议，请选择 Classic 或 FD"),
            _ => NiXnetApi.nxCANioMode_CAN
        };
        status = NiXnetApi.nxSetProperty(_rxSession,
            NiXnetApi.nxPropSession_IntfCanIoMode, 4, ref ioMode);
        NiXnetApi.CheckStatus(status);

        status = NiXnetApi.nxSetProperty(_txSession,
            NiXnetApi.nxPropSession_IntfCanIoMode, 4, ref ioMode);
        NiXnetApi.CheckStatus(status);

        // CAN FD / XL 数据段波特率
        if (config.Protocol != CanProtocolType.Classic)
        {
            uint dataBaudRate = (uint)config.DataBitRate;
            status = NiXnetApi.nxSetProperty(_rxSession,
                NiXnetApi.nxPropSession_IntfCanFdBaudRate, 4, ref dataBaudRate);
            NiXnetApi.CheckStatus(status);

            status = NiXnetApi.nxSetProperty(_txSession,
                NiXnetApi.nxPropSession_IntfCanFdBaudRate, 4, ref dataBaudRate);
            NiXnetApi.CheckStatus(status);
        }

        // 启动会话
        status = NiXnetApi.nxStart(_rxSession, NiXnetApi.nxScope_Normal);
        NiXnetApi.CheckStatus(status);

        status = NiXnetApi.nxStart(_txSession, NiXnetApi.nxScope_Normal);
        NiXnetApi.CheckStatus(status);

        _isConnected = true;
    }

    public void Close()
    {
        if (!_isConnected) return;

        NiXnetApi.nxStop(_rxSession, NiXnetApi.nxScope_Normal);
        NiXnetApi.nxStop(_txSession, NiXnetApi.nxScope_Normal);
        NiXnetApi.nxClear(_rxSession);
        NiXnetApi.nxClear(_txSession);
        _rxSession = 0;
        _txSession = 0;
        lock (_lock) _pendingFrames.Clear();

        _isConnected = false;
    }

    public void Write(CanMessage message)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        var frameBytes = BuildFrameBytes(message);
        var status = NiXnetApi.nxWriteFrame(_txSession, frameBytes, (uint)frameBytes.Length, 1.0);
        NiXnetApi.CheckStatus(status);
    }

    public CanMessage? Read(int timeoutMs, CancellationToken ct = default)
    {
        return ReadInternal(null, timeoutMs, ct);
    }

    public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default)
    {
        return ReadInternal(id, timeoutMs, ct);
    }

    private CanMessage? ReadInternal(uint? filterId, int timeoutMs, CancellationToken ct)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        var buffer = new byte[8192]; // NI-XNET 帧缓冲区
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (!ct.IsCancellationRequested)
        {
            // 先从缓存队列中查找匹配帧
            lock (_lock)
            {
                while (_pendingFrames.Count > 0)
                {
                    var pending = _pendingFrames.Dequeue();
                    if (filterId == null || pending.Id == filterId.Value)
                        return pending;
                }
            }

            double timeout = (deadline - DateTime.UtcNow).TotalSeconds;
            if (timeout <= 0) break;

            var status = NiXnetApi.nxReadFrame(_rxSession, buffer, (uint)buffer.Length, timeout, out uint bytesRead);

            if (status != 0 || bytesRead == 0)
                continue; // 超时或无数据，由 deadline 控制退出

            // nxReadFrame 一次可能返回多帧，全部解析入队，避免丢帧
            lock (_lock)
            {
                foreach (var frame in ParseFrames(buffer, (int)bytesRead))
                    _pendingFrames.Enqueue(frame);
            }
        }

        return null;
    }

    /// <summary>构建 NI-XNET 帧字节（Raw Frame 格式，nxFrameVar_t）</summary>
    private byte[] BuildFrameBytes(CanMessage message)
    {
        // NI-XNET Raw CAN Frame (nxFrameVar_t):
        // Bytes 0-7:   Timestamp (TX 时为 0)
        // Bytes 8-11:  Identifier (little-endian，bit29=扩展帧标志)
        // Byte 12:     Type
        // Byte 13:     Flags
        // Byte 14:     Info
        // Byte 15:     PayloadLength
        // Bytes 16+:   Payload（最少 8 字节，按 8 字节对齐填充）

        int payloadLen = message.Data.Length;
        int paddedPayload = Math.Max(8, (payloadLen + 7) & ~7);
        var frame = new byte[16 + paddedPayload];

        // Timestamp = 0 (for TX)
        // ID（扩展帧在 Identifier 的 bit29 置位）
        uint id = message.Id;
        if (message.FrameType == CanFrameType.Extended)
            id |= NiXnetApi.nxFrameId_CAN_IsExtended;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), id);

        // Type
        frame[12] = message.IsFd
            ? NiXnetApi.nxFrameType_CANFDBRS_Data
            : NiXnetApi.nxFrameType_CAN_Data;

        // Payload length
        frame[15] = (byte)payloadLen;

        // Data
        Buffer.BlockCopy(message.Data, 0, frame, 16, payloadLen);

        return frame;
    }

    /// <summary>解析缓冲区中的全部 NI-XNET 帧</summary>
    private IEnumerable<CanMessage> ParseFrames(byte[] buffer, int length)
    {
        int offset = 0;
        while (offset + 24 <= length)
        {
            int payloadLen = buffer[offset + 15];
            int paddedPayload = Math.Max(8, (payloadLen + 7) & ~7);
            int frameSize = 16 + paddedPayload;
            if (offset + frameSize > length) yield break;

            byte frameType = buffer[offset + 12];
            // 仅处理 CAN 数据帧，跳过总线错误帧/特殊帧等
            if (frameType is NiXnetApi.nxFrameType_CAN_Data
                or NiXnetApi.nxFrameType_CAN20_Data
                or NiXnetApi.nxFrameType_CANFD_Data
                or NiXnetApi.nxFrameType_CANFDBRS_Data)
            {
                var msg = new CanMessage();

                msg.TimestampNs = (long)BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(offset));

                uint rawId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset + 8));
                msg.FrameType = (rawId & NiXnetApi.nxFrameId_CAN_IsExtended) != 0
                    ? CanFrameType.Extended : CanFrameType.Standard;
                msg.Id = rawId & 0x1FFFFFFF;

                msg.IsFd = frameType is NiXnetApi.nxFrameType_CANFD_Data
                    or NiXnetApi.nxFrameType_CANFDBRS_Data;
                msg.IsXl = false; // NI-XNET 不支持 CAN XL

                int dataLen = Math.Min(payloadLen, length - offset - 16);
                msg.Data = new byte[dataLen];
                Buffer.BlockCopy(buffer, offset + 16, msg.Data, 0, dataLen);

                yield return msg;
            }

            offset += frameSize;
        }
    }

    public void Dispose()
    {
        Close();
    }
}
