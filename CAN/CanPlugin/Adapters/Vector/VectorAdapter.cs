using CAN.Models;

namespace CAN.Adapters.Vector;

/// <summary>Vector CAN 适配器实现（XL Driver Library）</summary>
public sealed class VectorAdapter : ICanAdapter
{
    private const string AppName = "xTestPlatform";

    private int _portHandle = -1;
    private ulong _accessMask;
    private bool _isConnected;
    private bool _isFd;
    private bool _driverOpened;

    public bool IsConnected => _isConnected;

    public void Open(CanAdapterConfig config)
    {
        if (_isConnected) throw new InvalidOperationException("CAN 通道已打开");
        if (config.Protocol == CanProtocolType.XL)
            throw new NotSupportedException("Vector 适配器暂不支持 CAN XL 协议");

        try
        {
            OpenInternal(config);
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "未找到 vxlapi64.dll，请安装 Vector XL Driver Library。" +
                "下载地址: https://www.vector.com/int/en/products/products-a-z/libraries-drivers/xl-driver-library/");
        }
    }

    private void OpenInternal(CanAdapterConfig config)
    {
        // Channel 参数为全局通道索引（0、1、2…），在 Vector Hardware Config 中查看
        if (!int.TryParse(config.Channel.Trim(), out int channelIndex) || channelIndex < 0)
            throw new ArgumentException($"无效的 Vector 通道 '{config.Channel}'，应为通道索引（0、1、2…）");

        _isFd = config.Protocol == CanProtocolType.FD;

        VectorXlApi.CheckStatus(VectorXlApi.OpenDriver());
        _driverOpened = true;

        _accessMask = 1UL << channelIndex;
        ulong permissionMask = _accessMask;
        uint interfaceVersion = _isFd ? VectorXlApi.XL_INTERFACE_VERSION_V4 : VectorXlApi.XL_INTERFACE_VERSION;

        VectorXlApi.CheckStatus(VectorXlApi.OpenPort(
            ref _portHandle, AppName, _accessMask, ref permissionMask,
            16384, interfaceVersion, VectorXlApi.XL_BUS_TYPE_CAN));

        if (permissionMask == 0)
            throw new InvalidOperationException($"无法获得 Vector 通道 {channelIndex} 的初始化权限（可能被其他应用占用）");

        if (_isFd)
        {
            var conf = BuildFdConf((uint)config.BaudRate, (uint)config.DataBitRate);
            VectorXlApi.CheckStatus(VectorXlApi.CanFdSetConfiguration(_portHandle, _accessMask, ref conf));
        }
        else
        {
            VectorXlApi.CheckStatus(VectorXlApi.CanSetChannelBitrate(_portHandle, _accessMask, (uint)config.BaudRate));
        }

        VectorXlApi.CheckStatus(VectorXlApi.ActivateChannel(
            _portHandle, _accessMask, VectorXlApi.XL_BUS_TYPE_CAN, VectorXlApi.XL_ACTIVATE_RESET_CLOCK));

        _isConnected = true;
    }

    /// <summary>构建 CAN FD 位时序配置（采样点约 80%，时钟 80 MHz）</summary>
    private static VectorXlApi.XLcanFdConf BuildFdConf(uint arbBitRate, uint dataBitRate)
    {
        return new VectorXlApi.XLcanFdConf
        {
            arbitrationBitRate = arbBitRate,
            sjwAbr = 1,
            tseg1Abr = CalcTseg1(arbBitRate),
            tseg2Abr = CalcTseg2(arbBitRate),
            dataBitRate = dataBitRate,
            sjwDbr = 1,
            tseg1Dbr = CalcTseg1(dataBitRate),
            tseg2Dbr = CalcTseg2(dataBitRate),
            reserved1 = new byte[2]
        };
    }

    private static uint CalcTseg1(uint baudRate)
    {
        uint tq = 80_000_000 / baudRate; // 80 MHz CAN 时钟
        return Math.Max(1, tq - CalcTseg2(baudRate) - 1);
    }

    private static uint CalcTseg2(uint baudRate)
    {
        uint tq = 80_000_000 / baudRate;
        return Math.Max(1, tq / 5); // 采样点约 80%
    }

    public void Close()
    {
        if (_isConnected)
        {
            VectorXlApi.DeactivateChannel(_portHandle, _accessMask);
            VectorXlApi.ClosePort(_portHandle);
            _portHandle = -1;
            _isConnected = false;
        }
        if (_driverOpened)
        {
            VectorXlApi.CloseDriver();
            _driverOpened = false;
        }
    }

    public void Write(CanMessage message)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        if (_isFd)
        {
            var ev = new VectorXlApi.XLcanTxEvent
            {
                tag = VectorXlApi.XL_CAN_EV_TAG_TX_MSG,
                reserved = new byte[3],
                reserved1 = new byte[7],
                canId = message.FrameType == CanFrameType.Extended
                    ? message.Id | VectorXlApi.XL_CAN_EXT_MSG_ID
                    : message.Id,
                msgFlags = message.IsFd
                    ? VectorXlApi.XL_CAN_TXMSG_FLAG_EDL | VectorXlApi.XL_CAN_TXMSG_FLAG_BRS
                    : 0,
                dlc = LengthToDlc(message.Data.Length),
                data = new byte[64]
            };
            Array.Copy(message.Data, ev.data, Math.Min(message.Data.Length, 64));

            uint sent = 0;
            VectorXlApi.CheckStatus(VectorXlApi.CanTransmitEx(_portHandle, _accessMask, 1, ref sent, ref ev));
        }
        else
        {
            var ev = new VectorXlApi.XLevent
            {
                tag = 10, // XL_TRANSMIT_MSG
                tagData = new VectorXlApi.XLcanMsg
                {
                    id = message.FrameType == CanFrameType.Extended
                        ? message.Id | VectorXlApi.XL_CAN_EXT_MSG_ID
                        : message.Id,
                    dlc = (ushort)Math.Min(message.Data.Length, 8),
                    data = new byte[8]
                }
            };
            Array.Copy(message.Data, ev.tagData.data, Math.Min(message.Data.Length, 8));

            uint count = 1;
            VectorXlApi.CheckStatus(VectorXlApi.CanTransmit(_portHandle, _accessMask, ref count, ref ev));
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
        var ev = new VectorXlApi.XLevent();
        uint count = 1;
        var status = VectorXlApi.Receive(_portHandle, ref count, ref ev);
        if (status == VectorXlApi.XL_ERR_QUEUE_IS_EMPTY || count == 0) return null;
        VectorXlApi.CheckStatus(status);

        if (ev.tag != VectorXlApi.XL_RECEIVE_MSG) return null;

        bool isExtended = (ev.tagData.id & VectorXlApi.XL_CAN_EXT_MSG_ID) != 0;
        int len = Math.Min(ev.tagData.dlc, (ushort)8);
        var data = new byte[len];
        Array.Copy(ev.tagData.data, data, len);
        return new CanMessage
        {
            Id = ev.tagData.id & ~VectorXlApi.XL_CAN_EXT_MSG_ID,
            FrameType = isExtended ? CanFrameType.Extended : CanFrameType.Standard,
            Data = data,
            TimestampNs = (long)ev.timeStamp
        };
    }

    private CanMessage? ReadOneFd()
    {
        var ev = new VectorXlApi.XLcanRxEvent();
        var status = VectorXlApi.CanReceive(_portHandle, ref ev);
        if (status == VectorXlApi.XL_ERR_QUEUE_IS_EMPTY) return null;
        VectorXlApi.CheckStatus(status);

        if (ev.tag != VectorXlApi.XL_CAN_EV_TAG_RX_OK) return null;

        bool isExtended = (ev.canId & VectorXlApi.XL_CAN_EXT_MSG_ID) != 0;
        int len = DlcToLength(ev.dlc);
        var data = new byte[len];
        Array.Copy(ev.data, data, len);
        return new CanMessage
        {
            Id = ev.canId & ~VectorXlApi.XL_CAN_EXT_MSG_ID,
            FrameType = isExtended ? CanFrameType.Extended : CanFrameType.Standard,
            Data = data,
            IsFd = (ev.msgFlags & VectorXlApi.XL_CAN_RXMSG_FLAG_EDL) != 0,
            TimestampNs = (long)ev.timeStampSync
        };
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
