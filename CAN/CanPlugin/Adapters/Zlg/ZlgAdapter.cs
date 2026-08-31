using CAN.Models;

namespace CAN.Adapters.Zlg;

/// <summary>周立功 ZLG CAN 适配器实现（zlgcan.dll）</summary>
public sealed class ZlgAdapter : ICanAdapter
{
    private IntPtr _deviceHandle = IntPtr.Zero;
    private IntPtr _channelHandle = IntPtr.Zero;
    private bool _isConnected;
    private bool _isFd;

    public bool IsConnected => _isConnected;

    public void Open(CanAdapterConfig config)
    {


        try
        {
            OpenInternal(config);
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "未找到 zlgcan.dll，请将周立功 ZLGCAN 二次开发库（x64，含 kerneldlls 子目录）放入插件目录下的 Native\\Zlg 文件夹，" +
                "或安装 ZLGCAN 驱动并将其所在目录加入 PATH。" +
                "下载地址: https://www.zlg.cn/can/down/down/id/22.html" +
                Environment.NewLine + "诊断信息：" + ZlgApi.GetLoadDiagnostics());
        }
    }

    private void OpenInternal(CanAdapterConfig config)
    {
        // Channel 格式：<设备类型>/<设备索引>/<通道索引>，如 USBCAN2/0/0
        var parts = config.Channel.Trim().Split('/');
        if (parts.Length != 3 ||
            !uint.TryParse(parts[1], out uint deviceIndex) ||
            !uint.TryParse(parts[2], out uint channelIndex))
        {
            throw new ArgumentException(
                $"无效的 ZLG 通道 '{config.Channel}'，格式应为 <设备类型>/<设备索引>/<通道索引>，如 USBCAN2/0/0");
        }

        uint deviceType = ZlgApi.ParseDeviceType(parts[0]);
        _isFd = config.Protocol == CanProtocolType.FD;

        _deviceHandle = ZlgApi.OpenDevice(deviceType, deviceIndex, 0);
        if (_deviceHandle == IntPtr.Zero)
            throw new InvalidOperationException($"打开 ZLG 设备失败：{parts[0]} 索引 {deviceIndex}（请检查设备连接和驱动安装）");

        var initConfig = new ZlgApi.ZCAN_CHANNEL_INIT_CONFIG
        {
            can_type = _isFd ? ZlgApi.ZCAN_TYPE_CANFD : ZlgApi.ZCAN_TYPE_CAN
        };

        if (ZlgApi.IsCanFdDevice(deviceType))
        {
            // USBCANFD 系列：波特率必须在 InitCAN 之前通过设备属性设置，
            // 直接把 bps 填入 abit_timing/dbit_timing 会得到无效时序，导致后续发送失败。
            if (!ZlgApi.TrySetProperty(_deviceHandle, $"{channelIndex}/canfd_abit_baud_rate",
                    config.BaudRate.ToString()))
            {
                ZlgApi.CloseDevice(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    $"设置 ZLG 通道 {channelIndex} 仲裁段波特率 {config.BaudRate} bps 失败，" +
                    "请确认设备类型与实际硬件型号一致、通道索引有效，且该波特率在设备时钟下受支持");
            }

            int dataBitRate = _isFd ? config.DataBitRate : config.BaudRate;
            if (!ZlgApi.TrySetProperty(_deviceHandle, $"{channelIndex}/canfd_dbit_baud_rate",
                    dataBitRate.ToString()))
            {
                ZlgApi.CloseDevice(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    $"设置 ZLG 通道 {channelIndex} 数据段波特率 {dataBitRate} bps 失败，" +
                    "请确认该波特率在设备时钟下受支持");
            }

            initConfig.canfd.acc_code = 0;
            initConfig.canfd.acc_mask = 0xFFFFFFFF;
            initConfig.canfd.filter = 0;
            initConfig.canfd.mode = 0;
        }
        else if (_isFd)
        {
            // 非 USBCANFD 系列不支持 CAN FD
            ZlgApi.CloseDevice(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
            throw new InvalidOperationException($"ZLG 设备类型 {parts[0]} 不支持 CAN FD，请改用 USBCANFD 系列设备或将协议设为 Classic");
        }
        else
        {
            initConfig.can.acc_code = 0;
            initConfig.can.acc_mask = 0xFFFFFFFF;
            initConfig.can.filter = 0;
            initConfig.can.mode = 0;
            (initConfig.can.timing0, initConfig.can.timing1) = ZlgApi.ToTiming(config.BaudRate);
        }

        _channelHandle = ZlgApi.InitCAN(_deviceHandle, channelIndex, ref initConfig);
        if (_channelHandle == IntPtr.Zero)
        {
            ZlgApi.CloseDevice(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
            throw new InvalidOperationException($"初始化 ZLG CAN 通道 {channelIndex} 失败");
        }

        if (ZlgApi.StartCAN(_channelHandle) != ZlgApi.STATUS_OK)
        {
            ZlgApi.CloseDevice(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
            _channelHandle = IntPtr.Zero;
            throw new InvalidOperationException($"启动 ZLG CAN 通道 {channelIndex} 失败");
        }

        _isConnected = true;
    }

    public void Close()
    {
        if (!_isConnected) return;

        if (_channelHandle != IntPtr.Zero)
        {
            ZlgApi.ResetCAN(_channelHandle);
            _channelHandle = IntPtr.Zero;
        }
        if (_deviceHandle != IntPtr.Zero)
        {
            ZlgApi.CloseDevice(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
        }
        _isConnected = false;
    }

    public void Write(CanMessage message)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        uint canId = message.FrameType == CanFrameType.Extended
            ? message.Id | ZlgApi.CAN_EFF_FLAG
            : message.Id;

        if (_isFd && message.IsFd)
        {
            int len = Math.Min(message.Data.Length, 64);
            byte dlcLen = ToFdLength(len); // CAN FD 只允许特定长度，不足处补 0
            var data = new ZlgApi.ZCAN_TransmitFD_Data
            {
                frame = new ZlgApi.canfd_frame
                {
                    can_id = canId,
                    len = dlcLen,
                    flags = ZlgApi.CANFD_BRS,
                    data = new byte[64]
                },
                transmit_type = 0
            };
            Array.Copy(message.Data, data.frame.data, len);

            if (ZlgApi.TransmitFD(_channelHandle, ref data, 1) != 1)
                throw new InvalidOperationException(
                    $"ZLG CANFD 报文发送失败（ID=0x{message.Id:X}，长度={dlcLen}），请检查总线连接、终端电阻及波特率配置");
        }
        else
        {
            var data = new ZlgApi.ZCAN_Transmit_Data
            {
                frame = new ZlgApi.can_frame
                {
                    can_id = canId,
                    can_dlc = (byte)Math.Min(message.Data.Length, 8),
                    data = new byte[8]
                },
                transmit_type = 0
            };
            Array.Copy(message.Data, data.frame.data, Math.Min(message.Data.Length, 8));

            if (ZlgApi.Transmit(_channelHandle, ref data, 1) != 1)
                throw new InvalidOperationException(
                    $"ZLG CAN 报文发送失败（ID=0x{message.Id:X}，长度={data.frame.can_dlc}），请检查总线连接、终端电阻及波特率配置");
        }
    }

    /// <summary>把实际字节数向上取整为 CAN FD 允许的帧长度</summary>
    private static byte ToFdLength(int length) => length switch
    {
        <= 8 => (byte)length,
        <= 12 => 12,
        <= 16 => 16,
        <= 20 => 20,
        <= 24 => 24,
        <= 32 => 32,
        <= 48 => 48,
        _ => 64
    };

    public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => ReadInternal(null, timeoutMs, ct);

    public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => ReadInternal(id, timeoutMs, ct);

    private CanMessage? ReadInternal(uint? filterId, int timeoutMs, CancellationToken ct)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            int remainMs = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remainMs <= 0) break;

            CanMessage? msg = _isFd ? ReadOneFd(Math.Min(remainMs, 100)) : ReadOneClassic(Math.Min(remainMs, 100));
            if (msg != null)
            {
                if (filterId == null || msg.Id == filterId.Value)
                    return msg;
                // ID 不匹配，继续读取
            }
        }
        return null;
    }

    private CanMessage? ReadOneClassic(int waitMs)
    {
        var data = new ZlgApi.ZCAN_Receive_Data();
        if (ZlgApi.Receive(_channelHandle, ref data, 1, waitMs) != 1) return null;

        bool isExtended = (data.frame.can_id & ZlgApi.CAN_EFF_FLAG) != 0;
        var payload = new byte[data.frame.can_dlc];
        Array.Copy(data.frame.data, payload, data.frame.can_dlc);
        return new CanMessage
        {
            Id = data.frame.can_id & ~ZlgApi.CAN_EFF_FLAG,
            FrameType = isExtended ? CanFrameType.Extended : CanFrameType.Standard,
            Data = payload,
            TimestampNs = (long)data.timestamp * 1_000
        };
    }

    private CanMessage? ReadOneFd(int waitMs)
    {
        var data = new ZlgApi.ZCAN_ReceiveFD_Data();
        if (ZlgApi.ReceiveFD(_channelHandle, ref data, 1, waitMs) != 1) return null;

        bool isExtended = (data.frame.can_id & ZlgApi.CAN_EFF_FLAG) != 0;
        var payload = new byte[data.frame.len];
        Array.Copy(data.frame.data, payload, data.frame.len);
        return new CanMessage
        {
            Id = data.frame.can_id & ~ZlgApi.CAN_EFF_FLAG,
            FrameType = isExtended ? CanFrameType.Extended : CanFrameType.Standard,
            Data = payload,
            IsFd = true,
            TimestampNs = (long)data.timestamp * 1_000
        };
    }

    public void Dispose() => Close();
}
