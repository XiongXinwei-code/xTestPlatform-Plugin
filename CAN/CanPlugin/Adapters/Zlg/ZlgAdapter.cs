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
        if (_isConnected) throw new InvalidOperationException("CAN 通道已打开");
        if (config.Protocol == CanProtocolType.XL)
            throw new NotSupportedException("ZLG 适配器不支持 CAN XL 协议");

        try
        {
            OpenInternal(config);
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "未找到 zlgcan.dll，请安装周立功 ZLGCAN 驱动及二次开发库，并将 zlgcan.dll 所在目录加入 PATH。" +
                "下载地址: https://www.zlg.cn/can/down/down/id/22.html");
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
            can_type = _isFd ? ZlgApi.ZCAN_TYPE_CANFD : ZlgApi.ZCAN_TYPE_CAN,
            acc_code = 0,
            acc_mask = 0xFFFFFFFF,
            filter = 0,
            mode = 0
        };

        if (_isFd)
        {
            // CANFD 设备时序由设备属性设置决定，这里使用时序字段直接传入波特率（USBCANFD 系列支持）
            initConfig.abit_timing = (uint)config.BaudRate;
            initConfig.dbit_timing = (uint)config.DataBitRate;
        }
        else
        {
            (initConfig.timing0, initConfig.timing1) = ZlgApi.ToTiming(config.BaudRate);
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
            var data = new ZlgApi.ZCAN_TransmitFD_Data
            {
                frame = new ZlgApi.canfd_frame
                {
                    can_id = canId,
                    len = (byte)Math.Min(message.Data.Length, 64),
                    flags = ZlgApi.CANFD_BRS,
                    data = new byte[64]
                },
                transmit_type = 0
            };
            Array.Copy(message.Data, data.frame.data, Math.Min(message.Data.Length, 64));

            if (ZlgApi.TransmitFD(_channelHandle, ref data, 1) != 1)
                throw new InvalidOperationException("ZLG CANFD 报文发送失败");
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
                throw new InvalidOperationException("ZLG CAN 报文发送失败");
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
