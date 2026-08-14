using System.Buffers.Binary;
using LIN.Models;

namespace LIN.Adapters.NiXnet;

/// <summary>NI-XNET LIN 适配器实现（基于 nixnet.dll P/Invoke）</summary>
public sealed class NiXnetLinAdapter : ILinAdapter
{
    private uint _rxSession;
    private uint _txSession;
    private bool _isConnected;
    private readonly object _lock = new();
    private readonly Queue<LinFrame> _pendingFrames = new();

    public bool IsConnected => _isConnected;

    public void Open(LinAdapterConfig config)
    {
        if (_isConnected) throw new InvalidOperationException("LIN 通道已打开");
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
            if (_rxSession != 0) { NiXnetLinApi.nxClear(_rxSession); _rxSession = 0; }
            if (_txSession != 0) { NiXnetLinApi.nxClear(_txSession); _txSession = 0; }
            throw;
        }
    }

    private void OpenInternal(LinAdapterConfig config)
    {
        var interfaceName = config.Channel; // 如 "LIN1"

        // 创建接收会话（Frame In Stream，使用内存数据库，无需 LDF 数据库文件）
        var status = NiXnetLinApi.nxCreateSession(
            NiXnetLinApi.InMemoryDatabase, "", "", interfaceName,
            NiXnetLinApi.nxMode_FrameInStream,
            out _rxSession);
        NiXnetLinApi.CheckStatus(status, "创建接收会话");

        // 创建发送会话（Frame Out Stream）
        status = NiXnetLinApi.nxCreateSession(
            NiXnetLinApi.InMemoryDatabase, "", "", interfaceName,
            NiXnetLinApi.nxMode_FrameOutStream,
            out _txSession);
        NiXnetLinApi.CheckStatus(status, "创建发送会话");

        // 设置波特率
        uint baudRate = (uint)config.BaudRate;
        status = NiXnetLinApi.nxSetProperty(_rxSession,
            NiXnetLinApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
        NiXnetLinApi.CheckStatus(status, "设置接收会话波特率");

        status = NiXnetLinApi.nxSetProperty(_txSession,
            NiXnetLinApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
        NiXnetLinApi.CheckStatus(status, "设置发送会话波特率");

        // 设置主/从节点（主节点才能主动发送帧头；属性类型为 U32，4 字节）
        uint isMaster = config.IsMaster ? 1u : 0u;
        status = NiXnetLinApi.nxSetProperty(_txSession,
            NiXnetLinApi.nxPropSession_IntfLINMaster, 4, ref isMaster);
        NiXnetLinApi.CheckStatus(status, "设置主/从节点");

        // 启动会话
        status = NiXnetLinApi.nxStart(_rxSession, NiXnetLinApi.nxScope_Normal);
        NiXnetLinApi.CheckStatus(status, "启动接收会话");

        status = NiXnetLinApi.nxStart(_txSession, NiXnetLinApi.nxScope_Normal);
        NiXnetLinApi.CheckStatus(status, "启动发送会话");

        _isConnected = true;
    }

    public void Close()
    {
        lock (_lock)
        {
            if (!_isConnected) return;
            try
            {
                if (_rxSession != 0) { NiXnetLinApi.nxStop(_rxSession, NiXnetLinApi.nxScope_Normal); NiXnetLinApi.nxClear(_rxSession); }
                if (_txSession != 0) { NiXnetLinApi.nxStop(_txSession, NiXnetLinApi.nxScope_Normal); NiXnetLinApi.nxClear(_txSession); }
            }
            finally
            {
                _rxSession = 0;
                _txSession = 0;
                _pendingFrames.Clear();
                _isConnected = false;
            }
        }
    }

    public void Write(LinFrame frame)
    {
        if (!_isConnected) throw new InvalidOperationException("LIN 通道未打开");

        var buf = BuildFrameBytes(frame);
        var status = NiXnetLinApi.nxWriteFrame(_txSession, buf, (uint)buf.Length, 1.0);
        NiXnetLinApi.CheckStatus(status);
    }

    public LinFrame? Read(int timeoutMs, CancellationToken ct = default)
        => ReadInternal(null, timeoutMs, ct);

    public LinFrame? Read(byte frameId, int timeoutMs, CancellationToken ct = default)
        => ReadInternal(frameId, timeoutMs, ct);

    private LinFrame? ReadInternal(byte? filterFrameId, int timeoutMs, CancellationToken ct)
    {
        if (!_isConnected) throw new InvalidOperationException("LIN 通道未打开");

        var buffer = new byte[8192];
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (!ct.IsCancellationRequested)
        {
            // 先从缓存队列中查找匹配帧
            lock (_lock)
            {
                while (_pendingFrames.Count > 0)
                {
                    var pending = _pendingFrames.Dequeue();
                    if (filterFrameId == null || pending.FrameId == filterFrameId.Value)
                        return pending;
                }
            }

            double timeout = (deadline - DateTime.UtcNow).TotalSeconds;
            if (timeout <= 0) break;

            var status = NiXnetLinApi.nxReadFrame(_rxSession, buffer, (uint)buffer.Length, timeout, out uint bytesRead);

            if (status == NiXnetLinApi.nxErrEventTimeout)
                continue; // 超时无数据，由 deadline 控制退出
            NiXnetLinApi.CheckStatus(status);
            if (bytesRead == 0)
                continue;

            // nxReadFrame 一次可能返回多帧，全部解析入队，避免丢帧
            lock (_lock)
            {
                foreach (var frame in ParseFrames(buffer, (int)bytesRead))
                    _pendingFrames.Enqueue(frame);
            }
        }

        ct.ThrowIfCancellationRequested();
        return null;
    }

    /// <summary>构建 NI-XNET 原始帧字节（nxFrameVar_t 格式）</summary>
    private static byte[] BuildFrameBytes(LinFrame frame)
    {
        // NI-XNET Raw LIN Frame (nxFrameVar_t):
        // Bytes 0-7:   Timestamp (TX 时为 0)
        // Bytes 8-11:  Identifier (little-endian，LIN 原始 ID 0-63，奇偶校验位由驱动计算)
        // Byte 12:     Type (LIN Data = 0x40)
        // Byte 13:     Flags
        // Byte 14:     Info
        // Byte 15:     PayloadLength
        // Bytes 16+:   Payload（最少 8 字节，按 8 字节对齐填充）

        int payloadLen = frame.Data.Length;
        int paddedPayload = Math.Max(8, (payloadLen + 7) & ~7);
        var buf = new byte[16 + paddedPayload];

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(8), (uint)(frame.FrameId & 0x3F));
        buf[12] = NiXnetLinApi.nxFrameType_LIN_Data;
        buf[15] = (byte)payloadLen;
        Array.Copy(frame.Data, 0, buf, 16, payloadLen);

        return buf;
    }

    /// <summary>解析 NI-XNET 原始帧字节流（可能包含多帧）</summary>
    private static IEnumerable<LinFrame> ParseFrames(byte[] buffer, int totalBytes)
    {
        int offset = 0;
        while (offset + 16 <= totalBytes)
        {
            long timestamp = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(offset));
            uint identifier = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset + 8));
            byte type = buffer[offset + 12];
            byte payloadLen = buffer[offset + 15];
            int paddedPayload = Math.Max(8, (payloadLen + 7) & ~7);

            if (offset + 16 + paddedPayload > totalBytes) yield break;

            if (type == NiXnetLinApi.nxFrameType_LIN_Data && payloadLen <= 8)
            {
                var data = new byte[payloadLen];
                Array.Copy(buffer, offset + 16, data, 0, payloadLen);

                yield return new LinFrame
                {
                    FrameId = (byte)(identifier & 0x3F),
                    Data = data,
                    TimestampNs = timestamp * 100L // NI-XNET 时间戳单位为 100ns
                };
            }

            offset += 16 + paddedPayload;
        }
    }

    public void Dispose() => Close();
}
