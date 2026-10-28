using System.Buffers.Binary;
using LIN.Models;

namespace LIN.Adapters.NiXnet;

/// <summary>NI-XNET LIN 适配器实现（基于 nixnet.dll P/Invoke）</summary>
public sealed class NiXnetLinAdapter : ILinAdapter, ILinCloseState, ILinOperationBoundary
{
    private readonly NiLinIo _io;
    public NiXnetLinAdapter() : this(new NiLinIo()) { }
    internal NiXnetLinAdapter(NiLinIo io) => _io = io;
    private uint _rxSession;
    private uint _txSession;
    private bool _isConnected;
    private readonly object _lock = new();
    private readonly LinkedList<LinFrame> _pendingFrames = new();
    private const int MaxPendingFrames = 4096;

    public bool IsConnected => _isConnected;
    public bool HandlesReleased => _rxSession == 0 && _txSession == 0;

    public void Open(LinAdapterConfig config) => Open(config, static () => { });
    void ILinOperationBoundary.Open(LinAdapterConfig config, Action beforeSubmit) => Open(config, beforeSubmit);
    private void Open(LinAdapterConfig config, Action beforeSubmit)
    {
        if (!HandlesReleased) throw new InvalidOperationException("LIN 通道已打开或上次释放尚未成功");
        try
        {
            OpenInternal(config, beforeSubmit);
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "未找到 nixnet.dll，请安装 NI-XNET 驱动程序。" +
                "下载地址: https://www.ni.com/zh-cn/support/downloads/drivers/download.ni-xnet.html");
        }
        catch (Exception ex)
        {
            try { Close(); }
            catch (Exception cleanup) { throw new AggregateException("LIN 打开失败且会话清理未完成", ex, cleanup); }
            throw;
        }
    }

    private void OpenInternal(LinAdapterConfig config, Action beforeSubmit)
    {
        // NI 官方 FrameOutStreamSession 契约：此模式仅支持主节点，未定义数据库帧使用 Enhanced。
        if (config.LinVersion != LinVersionType.LIN_2x || !config.IsMaster)
            throw new NotSupportedException("当前 NI-XNET 无 LDF 流模式仅支持 LIN 2.x 主节点；LIN 1.x/从节点需要数据库会话，本插件尚未实现。请改选 LIN 2.x、主节点。");
        var interfaceName = config.Channel; // 如 "LIN1"

        // 创建接收会话（Frame In Stream，使用内存数据库，无需 LDF 数据库文件）
        beforeSubmit();
        var status = _io.Create(
            NiXnetLinApi.InMemoryDatabase, "", "", interfaceName,
            NiXnetLinApi.nxMode_FrameInStream,
            out _rxSession);
        _io.Check(status, "创建接收会话");

        // 创建发送会话（Frame Out Stream）
        beforeSubmit();
        status = _io.Create(
            NiXnetLinApi.InMemoryDatabase, "", "", interfaceName,
            NiXnetLinApi.nxMode_FrameOutStream,
            out _txSession);
        _io.Check(status, "创建发送会话");

        // 设置波特率
        uint baudRate = (uint)config.BaudRate;
        beforeSubmit();
        status = _io.Set(_rxSession,
            NiXnetLinApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
        _io.Check(status, "设置接收会话波特率");

        beforeSubmit();
        status = _io.Set(_txSession,
            NiXnetLinApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
        _io.Check(status, "设置发送会话波特率");

        // 设置主/从节点（主节点才能主动发送帧头；属性类型为 U32，4 字节）
        uint isMaster = config.IsMaster ? 1u : 0u;
        beforeSubmit();
        status = _io.Set(_txSession,
            NiXnetLinApi.nxPropSession_IntfLINMaster, 4, ref isMaster);
        _io.Check(status, "设置主/从节点");

        // 设置接收队列大小（QueueSize 单位为字节；LIN 每帧按 nxFrameVar_t 24 字节估算，
        // 防止两次 Read 步骤之间驱动队列溢出丢帧）
        if (config.RxQueueSize > 0)
        {
            uint queueBytes = (uint)(config.RxQueueSize * 24);
            beforeSubmit();
            status = _io.Set(_rxSession,
                NiXnetLinApi.nxPropSession_QueueSize, 4, ref queueBytes);
            _io.Check(status, "设置接收队列大小");
        }

        // 启动会话
        beforeSubmit();
        status = _io.Start(_rxSession, NiXnetLinApi.nxScope_Normal);
        _io.Check(status, "启动接收会话");

        beforeSubmit();
        status = _io.Start(_txSession, NiXnetLinApi.nxScope_Normal);
        _io.Check(status, "启动发送会话");

        beforeSubmit();
        _isConnected = true;
    }

    public void Close()
    {
        lock (_lock)
        {
            _isConnected = false;
            var errors = new List<Exception>();
            Release(ref _txSession); Release(ref _rxSession);
            if (HandlesReleased) _pendingFrames.Clear();
            if (errors.Count != 0) throw new AggregateException("LIN 驱动停止或释放失败；未释放的句柄已保留，可重试关闭", errors);
            void Release(ref uint session)
            {
                if (session == 0) return;
                try { _io.Check(_io.Stop(session, NiXnetLinApi.nxScope_Normal), "停止会话"); }
                catch (Exception ex) { errors.Add(ex); }
                try { _io.Check(_io.Clear(session), "释放会话"); session = 0; }
                catch (Exception ex) { errors.Add(ex); }
            }
        }
    }

    public void Wakeup(bool remote = true) => Wakeup(remote, static () => { });
    void ILinOperationBoundary.Wakeup(bool remote, Action beforeSubmit) => Wakeup(remote, beforeSubmit);
    private void Wakeup(bool remote, Action beforeSubmit)
    {
        if (!_isConnected) throw new InvalidOperationException("LIN 通道未打开");

        // Interface:LIN:Sleep 属性是"状态转换请求"：RemoteWake 仅在接口处于睡眠态时
        // 才会发送总线唤醒模式（接口已唤醒时写入被忽略）。而 nxStart 后接口默认为唤醒态，
        // 因此需先写 LocalSleep（仅本地置睡眠，无总线信号）再写 RemoteWake，
        // 强制触发"睡眠→唤醒"转换，确保唤醒模式真正发送到总线上。
        if (remote)
        {
            uint localSleep = NiXnetLinApi.nxLINSleep_LocalSleep;
            beforeSubmit();
            var st = _io.Set(_txSession,
                NiXnetLinApi.nxPropSession_IntfLINSleep, 4, ref localSleep);
            _io.Check(st, "LIN 唤醒(预置本地睡眠)");

            uint remoteWake = NiXnetLinApi.nxLINSleep_RemoteWake;
            beforeSubmit();
            st = _io.Set(_txSession,
                NiXnetLinApi.nxPropSession_IntfLINSleep, 4, ref remoteWake);
            _io.Check(st, "LIN 唤醒(发送总线唤醒模式)");
        }
        else
        {
            uint localWake = NiXnetLinApi.nxLINSleep_LocalWake;
            beforeSubmit();
            var st = _io.Set(_txSession,
                NiXnetLinApi.nxPropSession_IntfLINSleep, 4, ref localWake);
            _io.Check(st, "LIN 唤醒(本地接口唤醒)");
        }
    }

    public void Sleep(bool remote = true)
    {
        if (!_isConnected) throw new InvalidOperationException("LIN 通道未打开");

        // RemoteSleep：主节点在总线上发送 Go-to-Sleep 命令（ID 0x3C，首字节 0x00），
        // 随后本地接口也进入睡眠态；LocalSleep：仅本地接口睡眠，无总线信号。
        // nxStart 后接口默认为唤醒态，“唤醒→睡眠”转换直接有效。
        uint sleepState = remote ? NiXnetLinApi.nxLINSleep_RemoteSleep : NiXnetLinApi.nxLINSleep_LocalSleep;
        var status = _io.Set(_txSession,
            NiXnetLinApi.nxPropSession_IntfLINSleep, 4, ref sleepState);
        _io.Check(status, remote ? "LIN 睡眠(发送 Go-to-Sleep)" : "LIN 睡眠(本地接口)");
    }

    public void Write(LinFrame frame)
    {
        if (!_isConnected) throw new InvalidOperationException("LIN 通道未打开");

        var buf = BuildFrameBytes(frame);
        var status = _io.Write(_txSession, buf, (uint)buf.Length, 1.0);
        _io.Check(status);
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
            // 先从缓存队列中查找匹配帧；不匹配的帧保留在队列中供后续读取
            lock (_lock)
            {
                for (var node = _pendingFrames.First; node != null; node = node.Next)
                {
                    if (filterFrameId == null || node.Value.FrameId == filterFrameId.Value)
                    {
                        _pendingFrames.Remove(node);
                        return node.Value;
                    }
                }
            }

            if (DateTime.UtcNow >= deadline) break;

            // 注意：nxReadFrame 的 timeout 语义是"等待直到填满整个缓冲区"，
            // 未填满时返回 nxErrEventTimeout，但已收到的帧仍通过 bytesRead 返回。
            // 因此这里使用 timeout=0（立即返回当前已有帧）+ 轮询模式，
            // 且无论状态如何，只要 bytesRead > 0 都必须解析，否则会丢帧。
            var status = _io.Read(_rxSession, buffer, (uint)buffer.Length, 0.0, out uint bytesRead);

            if (status != NiXnetLinApi.nxErrEventTimeout)
                _io.Check(status);

            if (bytesRead == 0)
            {
                Thread.Sleep(5); // 无数据时短暂让出 CPU，由 deadline 控制退出
                continue;
            }

            // nxReadFrame 一次可能返回多帧，全部解析入队，避免丢帧
            lock (_lock)
            {
                foreach (var frame in ParseFrames(buffer, (int)bytesRead))
                {
                    _pendingFrames.AddLast(frame);
                    // 缓存上限保护：超出时丢弃最旧的帧，避免长期不读取时内存无限增长
                    if (_pendingFrames.Count > MaxPendingFrames)
                        _pendingFrames.RemoveFirst();
                }
            }
        }

        ct.ThrowIfCancellationRequested();
        return null;
    }

    /// <summary>构建 NI-XNET 原始帧字节（nxFrameVar_t 格式）</summary>
    private static byte[] BuildFrameBytes(LinFrame frame)
    {
        if (frame.FrameId > 63 || frame.Data.Length > 8) throw new ArgumentException("LIN ID 必须在 0~63，载荷最多 8 字节");
        if (frame.Data.Length != 0 && (frame.ChecksumType != LinChecksumType.Enhanced || frame.FrameId is 0x3C or 0x3D))
            throw new NotSupportedException("当前无 LDF 流发送仅支持普通帧 Enhanced；Classic 和诊断帧需数据库校验定义，不能静默使用另一校验类型。空载荷仅发送帧头，不生成校验。");
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
