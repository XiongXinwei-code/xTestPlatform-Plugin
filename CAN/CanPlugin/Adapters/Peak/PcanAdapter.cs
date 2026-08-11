using CAN.Models;

namespace CAN.Adapters.Peak;

/// <summary>PEAK PCAN 适配器实现（PCAN-Basic API）</summary>
public sealed class PcanAdapter : ICanAdapter
{
    private ushort _channel;
    private bool _isConnected;
    private bool _isFd;

    public bool IsConnected => _isConnected;

    public void Open(CanAdapterConfig config)
    {
        if (_isConnected) throw new InvalidOperationException("CAN 通道已打开");
        if (config.Protocol == CanProtocolType.XL)
            throw new NotSupportedException("PEAK 适配器不支持 CAN XL 协议");

        try
        {
            OpenInternal(config);
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "未找到 PCANBasic.dll，请安装 PCAN-Basic API / PEAK 驱动程序。" +
                "下载地址: https://www.peak-system.com/PCAN-Basic.239.0.html");
        }
    }

    private void OpenInternal(CanAdapterConfig config)
    {
        _channel = PcanApi.ParseChannel(config.Channel);
        _isFd = config.Protocol == CanProtocolType.FD;

        if (_isFd)
        {
            // FD 模式使用位速率字符串（f_clock 80 MHz，简化为常用采样点配置）
            var bitrateFd =
                $"f_clock_mhz=80,nom_brp=2,nom_tseg1={CalcTseg1(80_000_000 / 2, config.BaudRate)},nom_tseg2={CalcTseg2(80_000_000 / 2, config.BaudRate)},nom_sjw=1," +
                $"data_brp=2,data_tseg1={CalcTseg1(80_000_000 / 2, config.DataBitRate)},data_tseg2={CalcTseg2(80_000_000 / 2, config.DataBitRate)},data_sjw=1";
            PcanApi.CheckStatus(PcanApi.InitializeFD(_channel, bitrateFd));
        }
        else
        {
            PcanApi.CheckStatus(PcanApi.Initialize(_channel, PcanApi.ToBtr0Btr1(config.BaudRate), 0, 0, 0));
        }

        _isConnected = true;
    }

    private static int CalcTseg1(int clock, int baudRate)
    {
        int tq = clock / baudRate; // 每位时间量子数
        return Math.Max(1, tq - CalcTseg2(clock, baudRate) - 1); // 1 为同步段
    }

    private static int CalcTseg2(int clock, int baudRate)
    {
        int tq = clock / baudRate;
        return Math.Max(1, tq / 5); // 采样点约 80%
    }

    public void Close()
    {
        if (!_isConnected) return;
        PcanApi.Uninitialize(_channel);
        _isConnected = false;
    }

    public void Write(CanMessage message)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        if (_isFd)
        {
            var msg = new PcanApi.TPCANMsgFD
            {
                ID = message.Id,
                MSGTYPE = BuildMsgType(message),
                DLC = LengthToDlc(message.Data.Length),
                DATA = new byte[64]
            };
            Array.Copy(message.Data, msg.DATA, Math.Min(message.Data.Length, 64));
            PcanApi.CheckStatus(PcanApi.WriteFD(_channel, ref msg));
        }
        else
        {
            var msg = new PcanApi.TPCANMsg
            {
                ID = message.Id,
                MSGTYPE = BuildMsgType(message),
                LEN = (byte)Math.Min(message.Data.Length, 8),
                DATA = new byte[8]
            };
            Array.Copy(message.Data, msg.DATA, Math.Min(message.Data.Length, 8));
            PcanApi.CheckStatus(PcanApi.Write(_channel, ref msg));
        }
    }

    public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => ReadInternal(null, timeoutMs, ct);

    public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => ReadInternal(id, timeoutMs, ct);

    private CanMessage? ReadInternal(uint? filterId, int timeoutMs, CancellationToken ct)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            CanMessage? msg = _isFd ? ReadOneFd() : ReadOneClassic();
            if (msg != null)
            {
                if (filterId == null || msg.Id == filterId.Value)
                    return msg;
                continue; // ID 不匹配，继续读取
            }
            Thread.Sleep(1); // 接收队列为空，短暂等待
        }
        return null;
    }

    private CanMessage? ReadOneClassic()
    {
        var status = PcanApi.Read(_channel, out var msg, out var ts);
        if (status == PcanApi.PCAN_ERROR_QRCVEMPTY) return null;
        PcanApi.CheckStatus(status);

        var data = new byte[msg.LEN];
        Array.Copy(msg.DATA, data, msg.LEN);
        return new CanMessage
        {
            Id = msg.ID,
            FrameType = (msg.MSGTYPE & PcanApi.PCAN_MESSAGE_EXTENDED) != 0 ? CanFrameType.Extended : CanFrameType.Standard,
            Data = data,
            TimestampNs = ((long)ts.millis * 1_000 + ts.micros) * 1_000
        };
    }

    private CanMessage? ReadOneFd()
    {
        var status = PcanApi.ReadFD(_channel, out var msg, out ulong ts);
        if (status == PcanApi.PCAN_ERROR_QRCVEMPTY) return null;
        PcanApi.CheckStatus(status);

        int len = DlcToLength(msg.DLC);
        var data = new byte[len];
        Array.Copy(msg.DATA, data, len);
        return new CanMessage
        {
            Id = msg.ID,
            FrameType = (msg.MSGTYPE & PcanApi.PCAN_MESSAGE_EXTENDED) != 0 ? CanFrameType.Extended : CanFrameType.Standard,
            Data = data,
            IsFd = (msg.MSGTYPE & PcanApi.PCAN_MESSAGE_FD) != 0,
            TimestampNs = (long)ts * 1_000
        };
    }

    private byte BuildMsgType(CanMessage message)
    {
        byte type = message.FrameType == CanFrameType.Extended
            ? PcanApi.PCAN_MESSAGE_EXTENDED
            : PcanApi.PCAN_MESSAGE_STANDARD;
        if (_isFd && message.IsFd)
            type |= PcanApi.PCAN_MESSAGE_FD | PcanApi.PCAN_MESSAGE_BRS;
        return type;
    }

    /// <summary>数据长度转 CAN FD DLC 编码</summary>
    private static byte LengthToDlc(int length) => length switch
    {
        <= 8 => (byte)length,
        <= 12 => 9,
        <= 16 => 10,
        <= 20 => 11,
        <= 24 => 12,
        <= 32 => 13,
        <= 48 => 14,
        _ => 15
    };

    /// <summary>CAN FD DLC 编码转数据长度</summary>
    private static int DlcToLength(byte dlc) => dlc switch
    {
        <= 8 => dlc,
        9 => 12,
        10 => 16,
        11 => 20,
        12 => 24,
        13 => 32,
        14 => 48,
        _ => 64
    };

    public void Dispose() => Close();
}
