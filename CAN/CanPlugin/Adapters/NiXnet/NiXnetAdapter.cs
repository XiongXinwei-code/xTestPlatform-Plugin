using System.Buffers.Binary;
using CAN.Models;

namespace CAN.Adapters.NiXnet;

/// <summary>NI-XNET CAN 适配器实现</summary>
public sealed class NiXnetAdapter : ICanAdapter, ICanAdapterDiagnostics
{
    // nxFrameVar_t 的最大长度：16 字节头 + 64 字节 CAN FD 数据。
    private const int MaxRawFrameSize = 80;

    private uint _rxSession;
    private uint _txSession;
    private bool _isConnected;
    private CanProtocolType _protocol = CanProtocolType.Classic;
    private readonly object _lock = new();
    private readonly Queue<CanMessage> _pendingFrames = new();
    private string _lastReceiveDiagnostics = "尚未执行 NI-XNET 接收";
    private bool _echoTxEnabled;
    private int _echoTxConfigStatus;

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
        var interfaceName = config.Channel; // 如 "CAN1"
        _protocol = config.Protocol;

        // IO 模式通过特殊内存数据库名选择（Interface:CAN:I/O Mode 属性是只读的，不能直接写）
        string database = config.Protocol switch
        {
            CanProtocolType.FD => NiXnetApi.InMemoryDatabaseCanFdBrs,
            _ => NiXnetApi.InMemoryDatabase
        };

        // 创建接收会话（Frame In Stream，使用内存数据库，无需 FIBEX/DBC 数据库文件）
        var status = NiXnetApi.nxCreateSession(
            database, "", "", interfaceName,
            NiXnetApi.nxMode_FrameInStream,
            out _rxSession);
        NiXnetApi.CheckStatus(status);

        // 创建发送会话（Frame Out Stream）
        status = NiXnetApi.nxCreateSession(
            database, "", "", interfaceName,
            NiXnetApi.nxMode_FrameOutStream,
            out _txSession);
        NiXnetApi.CheckStatus(status);

        // 设置仲裁段波特率。自定义位时序必须使用 U64 属性；普通模式保留原 U32
        // 写法作为兼容回退，避免改变既有序列在旧硬件/旧驱动上的行为。
        if (config.ArbitrationBitTiming != null)
        {
            ulong customBaudRate = config.ArbitrationBitTiming.NiXnetBaudRate64;
            status = NiXnetApi.nxSetPropertyUInt64(_rxSession,
                NiXnetApi.nxPropSession_IntfBaudRate64, 8, ref customBaudRate);
            NiXnetApi.CheckStatus(status);

            status = NiXnetApi.nxSetPropertyUInt64(_txSession,
                NiXnetApi.nxPropSession_IntfBaudRate64, 8, ref customBaudRate);
            NiXnetApi.CheckStatus(status);
        }
        else
        {
            uint baudRate = (uint)config.BaudRate;
            status = NiXnetApi.nxSetProperty(_rxSession,
                NiXnetApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
            NiXnetApi.CheckStatus(status);

            status = NiXnetApi.nxSetProperty(_txSession,
                NiXnetApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
            NiXnetApi.CheckStatus(status);
        }

        // 仅在用户主动使能时写入，未勾选则沿用驱动默认关闭状态；这样不具备软件终端
        // 电阻能力的 NI 设备仍可使用。支持的硬件会在 CAN_H/CAN_L 间接入 120 Ω。
        if (config.EnableTermination)
        {
            uint termination = 1;
            status = NiXnetApi.nxSetProperty(_rxSession,
                NiXnetApi.nxPropSession_IntfCanTerm, 4, ref termination);
            if (status < 0)
                ThrowTerminationError(status);

            status = NiXnetApi.nxSetProperty(_txSession,
                NiXnetApi.nxPropSession_IntfCanTerm, 4, ref termination);
            if (status < 0)
                ThrowTerminationError(status);
        }

        // CAN FD 数据段波特率
        if (config.Protocol == CanProtocolType.FD)
        {
            uint dataBaudRate = (uint)config.DataBitRate;
            status = NiXnetApi.nxSetProperty(_rxSession,
                NiXnetApi.nxPropSession_IntfCanFdBaudRate, 4, ref dataBaudRate);
            NiXnetApi.CheckStatus(status);

            status = NiXnetApi.nxSetProperty(_txSession,
                NiXnetApi.nxPropSession_IntfCanFdBaudRate, 4, ref dataBaudRate);
            NiXnetApi.CheckStatus(status);
        }

        // 设置接收队列大小（QueueSize 单位为字节；每帧按 nxFrameVar_t 最大 24 字节估算，
        // CAN FD 按 16+64=80 字节；防止两次 Read 步骤之间驱动队列溢出丢帧）
        if (config.RxQueueSize > 0)
        {
            int bytesPerFrame = config.Protocol == CanProtocolType.FD ? 80 : 24;
            uint queueBytes = (uint)(config.RxQueueSize * bytesPerFrame);
            status = NiXnetApi.nxSetProperty(_rxSession,
                NiXnetApi.nxPropSession_QueueSize, 4, ref queueBytes);
            NiXnetApi.CheckStatus(status);
        }

        // 发送完成回显用于区分“nxWriteFrame 已入队”和“报文已在总线上发送完成”。
        // 某些旧驱动/硬件可能不支持该属性，诊断能力降级但不阻止通道打开。
        byte echoTx = 1;
        _echoTxConfigStatus = NiXnetApi.nxSetPropertyByte(
            _rxSession, NiXnetApi.nxPropSession_IntfEchoTx, 1, ref echoTx);
        _echoTxEnabled = _echoTxConfigStatus >= 0;

        // 启动会话
        status = NiXnetApi.nxStart(_rxSession, NiXnetApi.nxScope_Normal);
        NiXnetApi.CheckStatus(status);

        status = NiXnetApi.nxStart(_txSession, NiXnetApi.nxScope_Normal);
        NiXnetApi.CheckStatus(status);

        if (config.ArbitrationBitTiming != null)
        {
            config.AppliedArbitrationBitRate = config.ArbitrationBitTiming.ActualBitRate;
            config.AppliedArbitrationSamplePoint = config.ArbitrationBitTiming.SamplePoint;
        }

        _isConnected = true;
    }

    private static void ThrowTerminationError(int status)
    {
        try
        {
            NiXnetApi.CheckStatus(status);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "启用 NI-XNET 内置 120 Ω 终端电阻失败。请确认当前设备支持软件终端电阻；" +
                "不支持时请取消勾选并在总线两端外接 120 Ω 电阻。", ex);
        }
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
        _protocol = CanProtocolType.Classic;
        _echoTxEnabled = false;
        _echoTxConfigStatus = 0;
        lock (_lock)
        {
            _pendingFrames.Clear();
            _lastReceiveDiagnostics = "NI-XNET 通道已关闭";
        }

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

        // 一次读取最多一个最大 CAN FD Raw Frame。NI-XNET 不会返回半帧；相比 8192 字节
        // 大缓冲区，这能避免驱动等待“填满缓冲区”的语义干扰单帧 UDS 响应读取。
        var buffer = new byte[MaxRawFrameSize];
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        int readCalls = 0;
        long totalBytesRead = 0;
        int parsedFrames = 0;
        int filteredFrames = 0;
        int transmitEchoFrames = 0;
        int lastStatus = 0;
        string lastRawHeader = "";
        var observedIds = new List<uint>(8);
        var transmitEchoIds = new List<uint>(4);

        while (!ct.IsCancellationRequested)
        {
            // 先从缓存队列中查找匹配帧
            lock (_lock)
            {
                while (_pendingFrames.Count > 0)
                {
                    var pending = _pendingFrames.Dequeue();
                    if (filterId == null || pending.Id == filterId.Value)
                    {
                        SetReceiveDiagnostics(BuildReceiveDiagnostics(
                            filterId, timeoutMs, readCalls, totalBytesRead, parsedFrames,
                            filteredFrames, transmitEchoFrames, lastStatus, observedIds,
                            transmitEchoIds, lastRawHeader, "", true));
                        return pending;
                    }

                    filteredFrames++;
                }
            }

            if (DateTime.UtcNow >= deadline) break;

            // 注意：nxReadFrame 的 timeout 语义是"等待直到填满整个缓冲区"，
            // 未填满时返回 nxErrEventTimeout，但已收到的帧仍通过 bytesRead 返回。
            // 因此这里使用 timeout=0（立即返回当前已有帧）+ 轮询模式，
            // 且无论状态如何，只要 bytesRead > 0 都必须解析，否则会丢帧。
            var status = NiXnetApi.nxReadFrame(_rxSession, buffer, (uint)buffer.Length, 0.0, out uint bytesRead);
            readCalls++;
            lastStatus = status;
            totalBytesRead += bytesRead;

            if (status != NiXnetApi.nxErrEventTimeout)
                NiXnetApi.CheckStatus(status); // 其他错误直接抛出，警告忽略

            if (bytesRead == 0)
            {
                Thread.Sleep(5); // 无数据时短暂让出 CPU，由 deadline 控制退出
                continue;
            }

            lastRawHeader = Convert.ToHexString(buffer.AsSpan(0, Math.Min((int)bytesRead, 24)));

            // nxReadFrame 一次可能返回多帧，全部解析入队，避免丢帧
            lock (_lock)
            {
                foreach (var frame in ParseFrames(buffer, (int)bytesRead))
                {
                    parsedFrames++;
                    if (frame.IsTransmitEcho)
                    {
                        transmitEchoFrames++;
                        if (transmitEchoIds.Count < 4 && !transmitEchoIds.Contains(frame.Id))
                            transmitEchoIds.Add(frame.Id);
                        continue;
                    }

                    _pendingFrames.Enqueue(frame);
                    if (observedIds.Count < 8 && !observedIds.Contains(frame.Id))
                        observedIds.Add(frame.Id);
                }
            }
        }

        string canComm = GetCanCommDiagnostics();
        SetReceiveDiagnostics(BuildReceiveDiagnostics(
            filterId, timeoutMs, readCalls, totalBytesRead, parsedFrames,
            filteredFrames, transmitEchoFrames, lastStatus, observedIds,
            transmitEchoIds, lastRawHeader, canComm, false));
        return null;
    }

    public string GetReceiveDiagnostics()
    {
        lock (_lock) return _lastReceiveDiagnostics;
    }

    private void SetReceiveDiagnostics(string diagnostics)
    {
        lock (_lock) _lastReceiveDiagnostics = diagnostics;
    }

    private string BuildReceiveDiagnostics(
        uint? filterId,
        int timeoutMs,
        int readCalls,
        long totalBytesRead,
        int parsedFrames,
        int filteredFrames,
        int transmitEchoFrames,
        int lastStatus,
        IReadOnlyCollection<uint> observedIds,
        IReadOnlyCollection<uint> transmitEchoIds,
        string lastRawHeader,
        string canComm,
        bool matched)
    {
        string target = filterId.HasValue ? $"0x{filterId.Value:X}" : "任意";
        string ids = observedIds.Count == 0
            ? "无"
            : string.Join(",", observedIds.Select(id => $"0x{id:X}"));
        string echoIds = transmitEchoIds.Count == 0
            ? "无"
            : string.Join(",", transmitEchoIds.Select(id => $"0x{id:X}"));
        string result = matched ? "已匹配" : $"{timeoutMs} ms 超时";
        string diagnostics =
            $"NI-XNET接收{result}：目标ID={target}，读取调用={readCalls}，" +
            $"驱动返回={totalBytesRead}字节，解析={parsedFrames}帧，过滤={filteredFrames}帧，" +
            $"接收ID={ids}，发送完成回显={transmitEchoFrames}帧({echoIds})，" +
            $"回显监控={GetEchoMonitorDescription()}，最后状态=0x{unchecked((uint)lastStatus):X8}";

        if (!string.IsNullOrWhiteSpace(canComm))
            diagnostics += $"，{canComm}";

        if (!matched && totalBytesRead > 0 && parsedFrames == 0 && !string.IsNullOrEmpty(lastRawHeader))
            diagnostics += $"，原始头={lastRawHeader}";

        return diagnostics;
    }

    private string GetEchoMonitorDescription() => _echoTxEnabled
        ? "已启用"
        : $"不可用(0x{unchecked((uint)_echoTxConfigStatus):X8})";

    private string GetCanCommDiagnostics()
    {
        try
        {
            int status = NiXnetApi.nxReadState(
                _rxSession, NiXnetApi.nxState_CANComm, 4, out uint stateValue, out int fault);
            if (status < 0)
                return $"CAN通信状态读取失败=0x{unchecked((uint)status):X8}";

            int state = (int)(stateValue & 0x0F);
            int lastError = (int)((stateValue >> 8) & 0x0F);
            int txErrorCount = (int)((stateValue >> 16) & 0xFF);
            int rxErrorCount = (int)((stateValue >> 24) & 0xFF);
            bool transceiverError = ((stateValue >> 4) & 0x01) != 0;
            bool sleep = ((stateValue >> 5) & 0x01) != 0;

            string result =
                $"CAN状态={GetCanStateDescription(state)}，最后总线错误={GetCanErrorDescription(lastError)}，" +
                $"Tx错误计数={txErrorCount}，Rx错误计数={rxErrorCount}，" +
                $"收发器错误={transceiverError}，休眠={sleep}";
            if (fault != 0)
                result += $"，异步故障=0x{unchecked((uint)fault):X8}";
            return result;
        }
        catch (Exception ex)
        {
            return $"CAN通信状态读取异常={ex.Message}";
        }
    }

    private static string GetCanStateDescription(int state) => state switch
    {
        0 => "ErrorActive",
        1 => "ErrorPassive",
        2 => "BusOff",
        3 => "Init",
        _ => $"未知({state})"
    };

    private static string GetCanErrorDescription(int error) => error switch
    {
        0 => "None",
        1 => "Stuff",
        2 => "Form",
        3 => "ACK",
        4 => "Bit1",
        5 => "Bit0",
        6 => "CRC",
        _ => $"未知({error})"
    };

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
        int txLen = GetValidTxLength(payloadLen, message.IsFd); // 非法长度向上对齐并用0x00填充
        int paddedPayload = Math.Max(8, (txLen + 7) & ~7);
        var frame = new byte[16 + paddedPayload];

        if (message.IsFd && _protocol != CanProtocolType.FD)
            throw new InvalidOperationException(
                "NI-XNET 当前通道以 Classic 模式打开，不能发送 CAN FD 报文；请将 CAN_Open 的 Protocol 设为 FD");

        // Timestamp = 0 (for TX)
        // ID（扩展帧在 Identifier 的 bit29 置位）
        uint id = message.Id;
        if (message.FrameType == CanFrameType.Extended)
            id |= NiXnetApi.nxFrameId_CAN_IsExtended;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), id);

        // Type：FD+BRS 会话内发送经典 CAN 报文时，NI-XNET 必须使用
        // CAN20_Data (0x08)，不能使用 Classic 会话中的 CAN_Data (0x00)。
        // 否则 nxWriteFrame 可能不报错，但总线上不会形成 ECU 可响应的经典 CAN 请求。
        frame[12] = message.IsFd
            ? NiXnetApi.nxFrameType_CANFDBRS_Data
            : _protocol == CanProtocolType.FD
                ? NiXnetApi.nxFrameType_CAN20_Data
                : NiXnetApi.nxFrameType_CAN_Data;

        // Payload length（必须为合法 CAN/CAN FD 长度，不足部分已由零字节填充）
        frame[15] = (byte)txLen;

        // Data
        Buffer.BlockCopy(message.Data, 0, frame, 16, payloadLen);

        return frame;
    }

    /// <summary>校验并对齐到合法的 CAN/CAN FD 发送长度（0-8/12/16/20/24/32/48/64）</summary>
    private static int GetValidTxLength(int length, bool isFd)
    {
        if (!isFd)
        {
            if (length > 8)
                throw new ArgumentException($"CAN 经典帧数据长度不能超过 8 字节（当前 {length}）");
            return length;
        }

        return length switch
        {
            <= 8 => length,
            <= 12 => 12,
            <= 16 => 16,
            <= 20 => 20,
            <= 24 => 24,
            <= 32 => 32,
            <= 48 => 48,
            <= 64 => 64,
            _ => throw new ArgumentException($"CAN FD 帧数据长度不能超过 64 字节（当前 {length}）")
        };
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

                // XNET 时间戳为 1601-01-01 起的 100ns 计数（FILETIME），转为 Unix 纪元纳秒
                ulong rawTs = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(offset));
                msg.TimestampNs = rawTs > 116444736000000000UL
                    ? (long)(rawTs - 116444736000000000UL) * 100
                    : (long)rawTs * 100;

                uint rawId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset + 8));
                msg.FrameType = (rawId & NiXnetApi.nxFrameId_CAN_IsExtended) != 0
                    ? CanFrameType.Extended : CanFrameType.Standard;
                msg.Id = rawId & 0x1FFFFFFF;

                msg.IsFd = frameType is NiXnetApi.nxFrameType_CANFD_Data
                    or NiXnetApi.nxFrameType_CANFDBRS_Data;
                msg.IsTransmitEcho = (buffer[offset + 13] & NiXnetApi.nxFrameFlags_TransmitEcho) != 0;

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
