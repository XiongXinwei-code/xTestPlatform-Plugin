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
    }

    private void OpenInternal(CanAdapterConfig config)
    {
        _protocol = config.Protocol;
        var interfaceName = config.Channel; // 如 "CAN1"

        // 创建接收会话（Frame In Stream）
        var status = NiXnetApi.nxCreateSession(
            "", "", "", interfaceName,
            NiXnetApi.nxMode_FrameInStream,
            out _rxSession);
        NiXnetApi.CheckStatus(status);

        // 创建发送会话（Frame Out Stream）
        status = NiXnetApi.nxCreateSession(
            "", "", "", interfaceName,
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
            CanProtocolType.XL => NiXnetApi.nxCANioMode_CAN_XL,
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
        double timeout = timeoutMs / 1000.0;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            var status = NiXnetApi.nxReadFrame(_rxSession, buffer, (uint)buffer.Length, timeout, out uint bytesRead);

            if (status != 0 || bytesRead == 0)
                return null;

            // 解析帧
            var msg = ParseFrame(buffer, (int)bytesRead);
            if (msg == null) return null;

            if (filterId == null || msg.Id == filterId.Value)
                return msg;

            // ID 不匹配，继续读取
            timeout = (deadline - DateTime.UtcNow).TotalSeconds;
            if (timeout <= 0) break;
        }

        return null;
    }

    /// <summary>构建 NI-XNET 帧字节（Raw Frame 格式）</summary>
    private byte[] BuildFrameBytes(CanMessage message)
    {
        // NI-XNET Raw CAN Frame:
        // Bytes 0-7: Timestamp (8 bytes, 0 for TX)
        // Bytes 8-11: Identifier (4 bytes, little-endian)
        // Byte 12: Type (frame type flags)
        // Byte 13: Flags
        // Byte 14: Info (DLC for Classic, actual length for FD)
        // Byte 15: reserved
        // Bytes 16+: Payload

        int payloadLen = message.Data.Length;
        int frameSize = 16 + payloadLen;

        // 对齐到 8 字节边界
        int alignedSize = (frameSize + 7) & ~7;
        var frame = new byte[alignedSize];

        // Timestamp = 0 (for TX)
        // ID
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), message.Id);

        // Type flags
        byte typeFlags = 0;
        if (message.FrameType == CanFrameType.Extended)
            typeFlags |= 0x01; // Extended frame bit

        if (message.IsFd)
        {
            typeFlags |= NiXnetApi.nxFrameType_CAN_FD;
            typeFlags |= NiXnetApi.nxFrameType_CAN_BRS;
        }

        frame[12] = typeFlags;

        // Payload length
        frame[14] = (byte)payloadLen;

        // Data
        Buffer.BlockCopy(message.Data, 0, frame, 16, payloadLen);

        return frame;
    }

    /// <summary>解析 NI-XNET 帧字节为 CanMessage</summary>
    private CanMessage? ParseFrame(byte[] buffer, int length)
    {
        if (length < 16) return null;

        var msg = new CanMessage();

        // Timestamp
        msg.TimestampNs = (long)BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(0));

        // ID
        msg.Id = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(8));

        // Type
        byte typeFlags = buffer[12];
        msg.FrameType = (typeFlags & 0x01) != 0 ? CanFrameType.Extended : CanFrameType.Standard;
        msg.IsFd = (typeFlags & NiXnetApi.nxFrameType_CAN_FD) != 0;
        msg.IsXl = _protocol == CanProtocolType.XL;

        // Strip extended frame bit from ID
        if (msg.FrameType == CanFrameType.Extended)
            msg.Id &= 0x1FFFFFFF;

        // Payload length
        int payloadLen = buffer[14];
        if (16 + payloadLen > length) payloadLen = length - 16;

        msg.Data = new byte[payloadLen];
        Buffer.BlockCopy(buffer, 16, msg.Data, 0, payloadLen);

        return msg;
    }

    public void Dispose()
    {
        Close();
    }
}
