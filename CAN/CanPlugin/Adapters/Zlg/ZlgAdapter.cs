using CAN.Models;
using CAN.Helpers;

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

        if (_isFd)
        {
            if (!ZlgApi.IsCanFdDevice(deviceType))
            {
                // 非 USBCANFD 系列不支持 CAN FD
                ZlgApi.CloseDevice(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                throw new InvalidOperationException($"ZLG 设备类型 {parts[0]} 不支持 CAN FD，请改用 USBCANFD 系列设备或将协议设为 Classic");
            }

            if (Math.Abs(config.ArbitrationSamplePoint - 80d) > 0.01)
            {
                ZlgApi.CloseDevice(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    $"当前内置 ZLGCAN 的 CAN FD 标准波特率接口固定使用 80% 仲裁段采样点，" +
                    $"不能可靠表达 {config.ArbitrationSamplePoint:F2}%；请设为 80%，或提供当前设备型号对应的 ZLG 自定义波特率字符串规则。");
            }
            if (Math.Abs(config.DataSamplePoint - 80d) > 0.01)
            {
                ZlgApi.CloseDevice(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    $"当前内置 ZLGCAN 的 CAN FD 标准波特率接口固定使用 80% 数据段采样点，" +
                    $"不能可靠表达 {config.DataSamplePoint:F2}%；请设为 80%，或提供当前设备型号对应的 ZLG 自定义波特率字符串规则。");
            }

            // 新版 ZLGCAN 使用属性接口配置 CAN FD；部分旧版 DLL / 固件会拒绝
            // canfd_* 属性，但仍接受 InitCAN 结构体中的直接时序值（v1.0.19 的方式）。
            // 因此属性设置只作为优先路径，失败时退回直接时序，不能在此提前中止打开过程。
            bool fdPropertiesConfigured =
                ZlgApi.TrySetProperty(_deviceHandle, $"{channelIndex}/canfd_standard", "0") &
                ZlgApi.TrySetProperty(_deviceHandle, $"{channelIndex}/canfd_abit_baud_rate",
                    config.BaudRate.ToString()) &
                ZlgApi.TrySetProperty(_deviceHandle, $"{channelIndex}/canfd_dbit_baud_rate",
                    config.DataBitRate.ToString());

            if (!fdPropertiesConfigured)
            {
                initConfig.canfd.abit_timing = (uint)config.BaudRate;
                initConfig.canfd.dbit_timing = (uint)config.DataBitRate;
            }

            initConfig.canfd.acc_code = 0;
            initConfig.canfd.acc_mask = 0xFFFFFFFF;
            initConfig.canfd.filter = 0;
            initConfig.canfd.mode = 0;
        }
        else
        {
            // Classic CAN（包括 USBCANFD 硬件上的 CANopen）：使用 CAN 配置联合体。
            // 不设置 canfd_* 属性；旧版 ZLGCAN DLL 可能不支持该属性路径，而 Classic
            // 模式只需要 BTR0/BTR1 时序参数。
            initConfig.can.acc_code = 0;
            initConfig.can.acc_mask = 0xFFFFFFFF;
            initConfig.can.filter = 0;
            initConfig.can.mode = 0;
            (initConfig.can.timing0, initConfig.can.timing1) =
                ZlgApi.ToTiming(config.BaudRate, config.ArbitrationSamplePoint);
        }

        _channelHandle = ZlgApi.InitCAN(_deviceHandle, channelIndex, ref initConfig);
        if (_channelHandle == IntPtr.Zero)
        {
            ZlgApi.CloseDevice(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
            throw new InvalidOperationException($"初始化 ZLG CAN 通道 {channelIndex} 失败");
        }

        // ZLGCAN 属性名沿用厂商设备 XML 中的 initenal_resistance（原始拼写）。
        // 老设备不具备该属性时，未启用状态保持兼容；用户明确启用时必须确认已生效。
        bool terminationConfigured = ZlgApi.TrySetProperty(
            _deviceHandle, $"{channelIndex}/initenal_resistance", config.EnableTermination ? "1" : "0");
        if (config.EnableTermination && !terminationConfigured)
        {
            ZlgApi.ResetCAN(_channelHandle);
            ZlgApi.CloseDevice(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
            _channelHandle = IntPtr.Zero;
            throw new InvalidOperationException(
                $"启用 ZLG 通道 {channelIndex} 内置 120 Ω 终端电阻失败；请确认设备支持该属性，或取消勾选并外接电阻。");
        }

        if (ZlgApi.StartCAN(_channelHandle) != ZlgApi.STATUS_OK)
        {
            ZlgApi.CloseDevice(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
            _channelHandle = IntPtr.Zero;
            throw new InvalidOperationException($"启动 ZLG CAN 通道 {channelIndex} 失败");
        }

        if (_isFd)
        {
            config.AppliedArbitrationBitRate = config.BaudRate;
            config.AppliedArbitrationSamplePoint = 80;
            config.AppliedDataBitRate = config.DataBitRate;
            config.AppliedDataSamplePoint = 80;
        }
        else
        {
            var timing = CanSamplePointCalculator.CalculateSja1000(
                config.BaudRate, config.ArbitrationSamplePoint);
            config.AppliedArbitrationBitRate = timing.ActualBitRate;
            config.AppliedArbitrationSamplePoint = timing.SamplePoint;
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
