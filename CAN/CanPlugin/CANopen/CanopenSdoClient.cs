using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using CAN.Adapters;
using CAN.Models;

namespace CAN.CANopen;

/// <summary>SDO 中止异常（设备返回 Abort 报文）</summary>
public sealed class CanopenSdoAbortException : Exception
{
    /// <summary>CiA 301 定义的 32 位中止码</summary>
    public uint AbortCode { get; }

    /// <summary>用设备返回的中止码构造异常</summary>
    public CanopenSdoAbortException(uint code)
        : base($"SDO 中止: 0x{code:X8} {Describe(code)}") => AbortCode = code;

    /// <summary>将中止码转换为中文说明</summary>
    public static string Describe(uint code) => code switch
    {
        0x05030000 => "触发位未交替",
        0x05040000 => "SDO 协议超时",
        0x05040001 => "命令字无效或未知",
        0x05040002 => "块大小无效",
        0x05040003 => "序号无效",
        0x05040004 => "CRC 错误",
        0x05040005 => "内存不足",
        0x06010000 => "不支持访问该对象",
        0x06010001 => "试图读取只写对象",
        0x06010002 => "试图写入只读对象",
        0x06020000 => "对象字典中不存在该对象",
        0x06040041 => "对象不能映射到 PDO",
        0x06040042 => "映射对象的数量和长度超出 PDO 长度",
        0x06040043 => "参数不兼容",
        0x06040047 => "设备内部不兼容",
        0x06060000 => "硬件错误导致访问失败",
        0x06070010 => "数据类型不匹配，服务参数长度不匹配",
        0x06070012 => "数据类型不匹配，服务参数过长",
        0x06070013 => "数据类型不匹配，服务参数过短",
        0x06090011 => "子索引不存在",
        0x06090030 => "参数值超出范围",
        0x06090031 => "写入值过大",
        0x06090032 => "写入值过小",
        0x06090036 => "最大值小于最小值",
        0x060A0023 => "资源不可用",
        0x08000000 => "一般错误",
        0x08000020 => "数据无法传输或保存到应用",
        0x08000021 => "因本地控制，数据无法传输或保存",
        0x08000022 => "因设备当前状态，数据无法传输或保存",
        0x08000023 => "对象字典动态生成失败或不存在",
        0x08000024 => "无可用数据",
        _ => "未知中止码"
    };
}

/// <summary>CANopen 总线收发封装，对阻塞式适配器读取做软超时兜底</summary>
internal sealed class CanopenBus
{
    private readonly ICanAdapter _adapter;

    public CanopenBus(ICanAdapter adapter) => _adapter = adapter;

    /// <summary>以 11 位标准帧发送一帧报文</summary>
    public void Send(uint cobId, byte[] data) =>
        _adapter.Write(new CanMessage { Id = cobId, FrameType = CanFrameType.Standard, Data = data });

    /// <summary>接收指定 COB-ID 的报文，超时返回 null</summary>
    public async Task<CanMessage?> ReceiveAsync(uint cobId, int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) return null;
        // 适配器 Read 为阻塞调用，放到线程池执行，并额外留 1 s 余量做软超时，防止驱动不返回导致步骤卡死
        var readTask = Task.Run(() => _adapter.Read(cobId, timeoutMs, ct), ct);
        try
        {
            return await readTask.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs + 1000), ct);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }
}

/// <summary>CANopen SDO 客户端，支持快速、分段与块传输</summary>
internal sealed class CanopenSdoClient
{
    private const byte MaxBlockSize = 127;

    private readonly CanopenBus _bus;
    private readonly byte _nodeId;
    private readonly int _timeoutMs;

    public CanopenSdoClient(CanopenBus bus, byte nodeId, int timeoutMs)
    {
        _bus = bus;
        _nodeId = nodeId;
        _timeoutMs = timeoutMs;
    }

    /// <summary>客户端→服务器 SDO 请求 COB-ID（0x600 + NodeId）</summary>
    private uint TxCobId => 0x600u + _nodeId;
    /// <summary>服务器→客户端 SDO 响应 COB-ID（0x580 + NodeId）</summary>
    private uint RxCobId => 0x580u + _nodeId;

    // ── 读取（Upload） ──────────────────────────────────────────

    /// <summary>读取对象字典条目</summary>
    /// <param name="block">true 使用块上传；false 由服务器决定快速或分段上传</param>
    public async Task<byte[]> UploadAsync(ushort index, byte subIndex, bool block, CancellationToken ct)
    {
        if (!block)
        {
            // 0x40：上传初始化请求
            var resp = await RequestAsync(InitFrame(0x40, index, subIndex), ct);
            return await HandleUploadInitResponseAsync(resp, index, subIndex, ct);
        }

        // 0xA4：块上传初始化（支持 CRC），byte4 = 每块最大段数，byte5 = 协议切换阈值（0 表示不切换）
        var init = InitFrame(0xA4, index, subIndex);
        init[4] = MaxBlockSize;
        init[5] = 0;
        var blockResp = await RequestAsync(init, ct);

        // 服务器可能退回普通上传
        if ((blockResp[0] & 0xE0) == 0x40)
            return await HandleUploadInitResponseAsync(blockResp, index, subIndex, ct);

        if ((blockResp[0] & 0xE3) != 0xC0)
            throw Protocol(blockResp, "块上传初始化");
        CheckMultiplexer(blockResp, index, subIndex);

        bool crcSupported = (blockResp[0] & 0x04) != 0;
        int? size = (blockResp[0] & 0x02) != 0 ? BinaryPrimitives.ReadInt32LittleEndian(blockResp.AsSpan(4)) : null;

        // 0xA3：通知服务器开始发送数据段
        _bus.Send(TxCobId, [0xA3, 0, 0, 0, 0, 0, 0, 0]);

        var data = new List<byte>();
        bool finished = false;
        while (!finished)
        {
            // 每块段序号从 1 开始；只接受连续序号，乱序段丢弃并通过确认帧让服务器重传
            int expected = 1;
            while (true)
            {
                var seg = await ExpectAsync(ct, allowAbortCheck: false);
                int seq = seg[0] & 0x7F;
                bool last = (seg[0] & 0x80) != 0;
                if (seq == 0 && last && IsAbort(seg))
                    throw new CanopenSdoAbortException(BinaryPrimitives.ReadUInt32LittleEndian(seg.AsSpan(4)));

                if (seq == expected)
                {
                    data.AddRange(seg[1..8]);
                    expected++;
                    if (last) finished = true;
                }

                if (last || seq >= MaxBlockSize) break;
            }

            // 0xA2：块确认，byte1 = 最后正确接收的序号，byte2 = 下一块大小
            _bus.Send(TxCobId, [0xA2, (byte)(expected - 1), MaxBlockSize, 0, 0, 0, 0, 0]);
        }

        var end = await ExpectAsync(ct);
        if ((end[0] & 0xE3) != 0xC1)
            throw Protocol(end, "块上传结束");

        // 结束帧 bit4..2 = 最后一段中无效字节数，需从尾部去除填充
        int unused = (end[0] >> 2) & 0x07;
        if (unused > 0 && data.Count >= unused)
            data.RemoveRange(data.Count - unused, unused);

        var result = data.ToArray();
        if (crcSupported)
        {
            ushort crc = (ushort)(end[1] | (end[2] << 8));
            if (Crc16(result) != crc)
                throw new InvalidOperationException("SDO 块上传 CRC 校验失败");
        }

        // 0xA1：块上传结束确认
        _bus.Send(TxCobId, [0xA1, 0, 0, 0, 0, 0, 0, 0]);

        if (size.HasValue && size.Value != result.Length)
            throw new InvalidOperationException($"SDO 块上传长度不一致：声明 {size} 字节，实际 {result.Length} 字节");
        return result;
    }

    /// <summary>处理上传初始化响应：快速上传直接取数据，否则继续分段上传</summary>
    private async Task<byte[]> HandleUploadInitResponseAsync(byte[] resp, ushort index, byte subIndex, CancellationToken ct)
    {
        if ((resp[0] & 0xE0) != 0x40)
            throw Protocol(resp, "上传初始化");
        CheckMultiplexer(resp, index, subIndex);

        // e 位 = 1：快速上传，数据在 byte4..7；s 位 = 1 时 n 字段表示无效字节数
        if ((resp[0] & 0x02) != 0)
        {
            int n = (resp[0] & 0x01) != 0 ? 4 - ((resp[0] >> 2) & 0x03) : 4;
            return resp[4..(4 + n)];
        }

        int? size = (resp[0] & 0x01) != 0 ? BinaryPrimitives.ReadInt32LittleEndian(resp.AsSpan(4)) : null;
        var data = new List<byte>();
        // 分段上传：0x60 | t 请求下一段，每段触发位交替翻转，c 位 = 1 表示最后一段
        int toggle = 0;
        while (true)
        {
            var seg = await RequestAsync([(byte)(0x60 | (toggle << 4)), 0, 0, 0, 0, 0, 0, 0], ct);
            if ((seg[0] & 0xE0) != 0x00)
                throw Protocol(seg, "分段上传");
            if (((seg[0] >> 4) & 0x01) != toggle)
                throw new InvalidOperationException("SDO 分段上传触发位错误");

            int n = 7 - ((seg[0] >> 1) & 0x07);
            data.AddRange(seg[1..(1 + n)]);
            if ((seg[0] & 0x01) != 0) break;
            toggle ^= 1;
        }

        if (size.HasValue && size.Value != data.Count)
            throw new InvalidOperationException($"SDO 分段上传长度不一致：声明 {size} 字节，实际 {data.Count} 字节");
        return data.ToArray();
    }

    // ── 写入（Download） ────────────────────────────────────────

    /// <summary>写入对象字典条目</summary>
    /// <param name="block">true 使用块下载；false 时 ≤4 字节快速下载，否则分段下载</param>
    public async Task DownloadAsync(ushort index, byte subIndex, byte[] data, bool block, CancellationToken ct)
    {
        if (data.Length == 0)
            throw new ArgumentException("SDO 写入数据不能为空");

        if (block)
        {
            await BlockDownloadAsync(index, subIndex, data, ct);
            return;
        }

        if (data.Length <= 4)
        {
            // 0x23 | n<<2：快速下载，n = 4 - 有效字节数
            var frame = InitFrame((byte)(0x23 | ((4 - data.Length) << 2)), index, subIndex);
            Buffer.BlockCopy(data, 0, frame, 4, data.Length);
            var resp = await RequestAsync(frame, ct);
            if (resp[0] != 0x60) throw Protocol(resp, "快速下载");
            CheckMultiplexer(resp, index, subIndex);
            return;
        }

        // 0x21：分段下载初始化并声明数据总长度
        var init = InitFrame(0x21, index, subIndex);
        BinaryPrimitives.WriteInt32LittleEndian(init.AsSpan(4), data.Length);
        var initResp = await RequestAsync(init, ct);
        if (initResp[0] != 0x60) throw Protocol(initResp, "分段下载初始化");
        CheckMultiplexer(initResp, index, subIndex);

        int offset = 0;
        int toggle = 0;
        while (offset < data.Length)
        {
            int n = Math.Min(7, data.Length - offset);
            bool last = offset + n >= data.Length;
            var seg = new byte[8];
            // 段命令字：bit4 = 触发位，bit3..1 = 无效字节数，bit0 = 最后一段
            seg[0] = (byte)((toggle << 4) | ((7 - n) << 1) | (last ? 1 : 0));
            Buffer.BlockCopy(data, offset, seg, 1, n);
            var resp = await RequestAsync(seg, ct);
            if ((resp[0] & 0xE0) != 0x20)
                throw Protocol(resp, "分段下载");
            if (((resp[0] >> 4) & 0x01) != toggle)
                throw new InvalidOperationException("SDO 分段下载触发位错误");
            offset += n;
            toggle ^= 1;
        }
    }

    /// <summary>块下载：初始化 → 按块连续发送数据段 → 等待块确认 → 结束帧（含 CRC）</summary>
    private async Task BlockDownloadAsync(ushort index, byte subIndex, byte[] data, CancellationToken ct)
    {
        // 0xC6：块下载初始化（支持 CRC，声明长度）
        var init = InitFrame(0xC6, index, subIndex);
        BinaryPrimitives.WriteInt32LittleEndian(init.AsSpan(4), data.Length);
        var resp = await RequestAsync(init, ct);
        if ((resp[0] & 0xE3) != 0xA0)
            throw Protocol(resp, "块下载初始化");
        CheckMultiplexer(resp, index, subIndex);

        bool crcSupported = (resp[0] & 0x04) != 0;
        int blockSize = resp[4];
        if (blockSize is < 1 or > MaxBlockSize)
            throw new InvalidOperationException($"SDO 块大小无效: {blockSize}");

        int offset = 0;
        while (offset < data.Length)
        {
            int blockStart = offset;
            int pos = offset;
            int seq = 0;
            while (seq < blockSize && pos < data.Length)
            {
                ct.ThrowIfCancellationRequested();
                seq++;
                int n = Math.Min(7, data.Length - pos);
                bool last = pos + n >= data.Length;
                var seg = new byte[8];
                seg[0] = (byte)(seq | (last ? 0x80 : 0x00));
                Buffer.BlockCopy(data, pos, seg, 1, n);
                _bus.Send(TxCobId, seg);
                pos += n;
            }

            var ack = await ExpectAsync(ct);
            if ((ack[0] & 0xE3) != 0xA2)
                throw Protocol(ack, "块下载确认");

            // 服务器确认的序号小于已发序号时，从未确认的段开始重传
            int ackSeq = ack[1];
            offset = ackSeq >= seq ? pos : blockStart + ackSeq * 7;
            if (ack[2] is >= 1 and <= MaxBlockSize)
                blockSize = ack[2];
        }

        // 0xC1 | n<<2：块下载结束，n = 最后一段无效字节数，byte1..2 = CRC
        int lastBytes = ((data.Length - 1) % 7) + 1;
        var end = new byte[8];
        end[0] = (byte)(0xC1 | ((7 - lastBytes) << 2));
        if (crcSupported)
        {
            ushort crc = Crc16(data);
            end[1] = (byte)(crc & 0xFF);
            end[2] = (byte)(crc >> 8);
        }

        var endResp = await RequestAsync(end, ct);
        if ((endResp[0] & 0xE3) != 0xA1)
            throw Protocol(endResp, "块下载结束");
    }

    // ── 基础收发 ────────────────────────────────────────────────

    /// <summary>发送请求并等待一帧响应</summary>
    private async Task<byte[]> RequestAsync(byte[] frame, CancellationToken ct)
    {
        _bus.Send(TxCobId, frame);
        return await ExpectAsync(ct);
    }

    /// <summary>等待一帧 SDO 响应，补齐为 8 字节；收到 Abort 时抛出中止异常</summary>
    /// <param name="allowAbortCheck">块上传数据段中 0x80 可能是合法序号，需由调用方自行判断</param>
    private async Task<byte[]> ExpectAsync(CancellationToken ct, bool allowAbortCheck = true)
    {
        var msg = await _bus.ReceiveAsync(RxCobId, _timeoutMs, ct);
        if (msg == null)
            throw new TimeoutException($"节点 {_nodeId} 在 {_timeoutMs} ms 内未返回 SDO 响应");

        var data = new byte[8];
        Buffer.BlockCopy(msg.Data, 0, data, 0, Math.Min(8, msg.Data.Length));

        if (allowAbortCheck && IsAbort(data))
            throw new CanopenSdoAbortException(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)));
        return data;
    }

    /// <summary>命令字 0x80 为 SDO 中止传输</summary>
    private static bool IsAbort(byte[] frame) => frame[0] == 0x80;

    /// <summary>构造带 Index/SubIndex 多路复用器的初始化帧</summary>

    private static byte[] InitFrame(byte command, ushort index, byte subIndex) =>
        [command, (byte)(index & 0xFF), (byte)(index >> 8), subIndex, 0, 0, 0, 0];

    /// <summary>校验响应中的 Index/SubIndex 与请求一致</summary>
    private static void CheckMultiplexer(byte[] resp, ushort index, byte subIndex)
    {
        ushort respIndex = (ushort)(resp[1] | (resp[2] << 8));
        if (respIndex != index || resp[3] != subIndex)
            throw new InvalidOperationException(
                $"SDO 响应对象不匹配：期望 0x{index:X4}sub{subIndex}，实际 0x{respIndex:X4}sub{resp[3]}");
    }

    /// <summary>构造命令字无效的协议异常</summary>
    private static InvalidOperationException Protocol(byte[] frame, string stage) =>
        new($"SDO {stage}响应命令字无效: {BitConverter.ToString(frame).Replace("-", " ")}");

    /// <summary>CRC-16-CCITT（多项式 0x1021，初值 0），用于 SDO 块传输</summary>
    internal static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}

/// <summary>CANopen 数据类型与字节序列之间的编解码</summary>
internal static class CanopenDataCodec
{
    /// <summary>按数据类型把文本值编码为小端字节序列</summary>
    public static byte[] Encode(CanopenDataType type, string text)
    {
        text = text.Trim();
        switch (type)
        {
            case CanopenDataType.Boolean:
                return [ParseBool(text) ? (byte)1 : (byte)0];
            case CanopenDataType.Integer8:
                return [unchecked((byte)checked((sbyte)ParseSigned(text)))];
            case CanopenDataType.Integer16:
            {
                var b = new byte[2]; BinaryPrimitives.WriteInt16LittleEndian(b, checked((short)ParseSigned(text))); return b;
            }
            case CanopenDataType.Integer32:
            {
                var b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, checked((int)ParseSigned(text))); return b;
            }
            case CanopenDataType.Integer64:
            {
                var b = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, ParseSigned(text)); return b;
            }
            case CanopenDataType.Unsigned8:
                return [checked((byte)ParseUnsigned(text))];
            case CanopenDataType.Unsigned16:
            {
                var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, checked((ushort)ParseUnsigned(text))); return b;
            }
            case CanopenDataType.Unsigned32:
            {
                var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, checked((uint)ParseUnsigned(text))); return b;
            }
            case CanopenDataType.Unsigned64:
            {
                var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, ParseUnsigned(text)); return b;
            }
            case CanopenDataType.Real32:
            {
                var b = new byte[4]; BinaryPrimitives.WriteSingleLittleEndian(b, float.Parse(text, CultureInfo.InvariantCulture)); return b;
            }
            case CanopenDataType.Real64:
            {
                var b = new byte[8]; BinaryPrimitives.WriteDoubleLittleEndian(b, double.Parse(text, CultureInfo.InvariantCulture)); return b;
            }
            case CanopenDataType.VisibleString:
                return Encoding.UTF8.GetBytes(text);
            case CanopenDataType.OctetString:
                return CanopenExecutorHelper.ParseHex(text);
            default:
                throw new NotSupportedException($"不支持的数据类型: {type}");
        }
    }

    /// <summary>按数据类型把小端字节序列解码为 .NET 值（OctetString 返回十六进制字符串）</summary>
    public static object Decode(CanopenDataType type, byte[] data)
    {
        int? size = FixedSize(type);
        if (size.HasValue && data.Length < size.Value)
            throw new InvalidOperationException($"返回数据长度 {data.Length} 字节，不足 {type} 所需的 {size} 字节");

        return type switch
        {
            CanopenDataType.Boolean => data[0] != 0,
            CanopenDataType.Integer8 => unchecked((sbyte)data[0]),
            CanopenDataType.Integer16 => BinaryPrimitives.ReadInt16LittleEndian(data),
            CanopenDataType.Integer32 => BinaryPrimitives.ReadInt32LittleEndian(data),
            CanopenDataType.Integer64 => BinaryPrimitives.ReadInt64LittleEndian(data),
            CanopenDataType.Unsigned8 => data[0],
            CanopenDataType.Unsigned16 => BinaryPrimitives.ReadUInt16LittleEndian(data),
            CanopenDataType.Unsigned32 => BinaryPrimitives.ReadUInt32LittleEndian(data),
            CanopenDataType.Unsigned64 => BinaryPrimitives.ReadUInt64LittleEndian(data),
            CanopenDataType.Real32 => BinaryPrimitives.ReadSingleLittleEndian(data),
            CanopenDataType.Real64 => BinaryPrimitives.ReadDoubleLittleEndian(data),
            CanopenDataType.VisibleString => Encoding.UTF8.GetString(data).TrimEnd('\0'),
            CanopenDataType.OctetString => CanopenExecutorHelper.ToHex(data),
            _ => throw new NotSupportedException($"不支持的数据类型: {type}")
        };
    }

    /// <summary>固定长度类型的字节数；字符串类返回 null</summary>
    public static int? FixedSize(CanopenDataType type) => type switch
    {
        CanopenDataType.Boolean or CanopenDataType.Integer8 or CanopenDataType.Unsigned8 => 1,
        CanopenDataType.Integer16 or CanopenDataType.Unsigned16 => 2,
        CanopenDataType.Integer32 or CanopenDataType.Unsigned32 or CanopenDataType.Real32 => 4,
        CanopenDataType.Integer64 or CanopenDataType.Unsigned64 or CanopenDataType.Real64 => 8,
        _ => null
    };

    /// <summary>解析布尔文本（true/false/1/0）</summary>
    private static bool ParseBool(string text) => text.ToLowerInvariant() switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => throw new FormatException($"无法将 \"{text}\" 解析为布尔值")
    };

    /// <summary>解析有符号整数（支持 0x 前缀）</summary>
    private static long ParseSigned(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return unchecked((long)Convert.ToUInt64(text[2..], 16));
        return long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    /// <summary>解析无符号整数（支持 0x 前缀）</summary>
    private static ulong ParseUnsigned(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt64(text[2..], 16);
        return ulong.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}
