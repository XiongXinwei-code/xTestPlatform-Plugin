using CAN.Models;
using CAN.Helpers;

namespace CAN.Adapters.Zlg;

/// <summary>周立功 ZLG CAN 适配器实现（zlgcan.dll）</summary>
public sealed class ZlgAdapter : ICanAdapter, ICanAdapterDiagnostics
{
    private IntPtr _deviceHandle = IntPtr.Zero;
    private IntPtr _channelHandle = IntPtr.Zero;
    private bool _isConnected;
    private bool _isFd;
    private readonly CanReceiveDiagnostics _diagnostics = new("ZLG");

    public bool IsConnected => _isConnected;

    public static bool IsPcieCanFdChannel(string channel) =>
        ZlgApi.IsPcieCanFdDevice(ZlgApi.ParseDeviceType(channel.Split('/')[0]));

    public void Open(CanAdapterConfig config)
    {
        try
        {
            OpenInternal(config);
        }
        catch (DllNotFoundException)
        {
            CleanupFailedOpen();
            throw new InvalidOperationException(
                "未找到 zlgcan.dll，请将周立功 ZLGCAN 二次开发库（x64，含 kerneldlls 子目录）放入插件目录下的 Native\\Zlg 文件夹，" +
                "或安装 ZLGCAN 驱动并将其所在目录加入 PATH。" +
                "下载地址: https://www.zlg.cn/can/down/down/id/22.html" +
                Environment.NewLine + "诊断信息：" + ZlgApi.GetLoadDiagnostics());
        }
        catch
        {
            CleanupFailedOpen();
            throw;
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
        bool isPcieFd = ZlgApi.IsPcieCanFdDevice(deviceType);
        bool driverClassicBitRate = ZlgApi.UsesDriverClassicBitRate(deviceType);
        bool useCanFdInit = _isFd || ZlgApi.UsesCanFdOnlyController(deviceType);
        double dataSamplePoint = ZlgApi.GetStandardDataSamplePoint(deviceType, config.DataBitRate);

        if (_isFd && !ZlgApi.IsCanFdDevice(deviceType))
            throw new InvalidOperationException($"ZLG 设备类型 {parts[0]} 不支持 CAN FD，请使用支持 CAN FD 的设备或将协议设为 Classic");
        if ((_isFd || isPcieFd) &&
            (!double.IsFinite(config.ArbitrationSamplePoint) ||
             Math.Abs(config.ArbitrationSamplePoint - 80d) > 0.01))
        {
            throw new InvalidOperationException(
                $"ZLG 设备 {parts[0]} 的标准波特率接口仅支持 80% 仲裁段采样点；" +
                $"不能可靠表达 {config.ArbitrationSamplePoint:F2}%，请设为 80% 或提供厂商自定义时序规则。");
        }
        if (_isFd &&
            (!double.IsFinite(config.DataSamplePoint) ||
             Math.Abs(config.DataSamplePoint - dataSamplePoint) > 0.01))
        {
            throw new InvalidOperationException(
                $"ZLG 设备 {parts[0]} 在 {config.DataBitRate} bps 标准数据段波特率下使用 {dataSamplePoint:F0}% 采样点；" +
                $"不能可靠表达 {config.DataSamplePoint:F2}%，请设为 {dataSamplePoint:F0}% 或提供厂商自定义时序规则。");
        }
        if (isPcieFd)
            ZlgApi.ValidatePcieBitRates(deviceType, config.BaudRate, _isFd ? config.DataBitRate : null);
        if (driverClassicBitRate && config.BaudRate is not
            (1_000_000 or 800_000 or 500_000 or 250_000 or 125_000 or
             100_000 or 50_000 or 20_000 or 10_000 or 5_000))
        {
            throw new ArgumentException($"ZLG 设备 {parts[0]} 不支持 {config.BaudRate} bps 标准波特率，请使用设备 XML 中的标准波特率。");
        }

        _deviceHandle = ZlgApi.OpenDevice(deviceType, deviceIndex, 0);
        if (_deviceHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"打开 ZLG 设备失败：{parts[0]} 索引 {deviceIndex}（请检查设备连接和驱动安装）" +
                Environment.NewLine + ZlgApi.GetDeviceOpenDiagnostics(deviceType));

        var initConfig = new ZlgApi.ZCAN_CHANNEL_INIT_CONFIG
        {
            can_type = useCanFdInit ? ZlgApi.ZCAN_TYPE_CANFD : ZlgApi.ZCAN_TYPE_CAN
        };

        if (isPcieFd)
        {
            // PCIe XML 使用 protocol/canfd_exp，不支持 USB 的 canfd_standard 属性。
            SetRequiredProperty(channelIndex, "protocol", useCanFdInit ? "1" : "0");
            SetRequiredProperty(channelIndex, "canfd_exp", _isFd ? "1" : "0");
            SetRequiredProperty(channelIndex, "canfd_abit_baud_rate", config.BaudRate.ToString());
            if (_isFd)
                SetRequiredProperty(channelIndex, "canfd_dbit_baud_rate", config.DataBitRate.ToString());

            if (useCanFdInit)
            {
                initConfig.canfd.acc_mask = 0xFFFFFFFF;
            }
            else
            {
                initConfig.can.acc_mask = 0xFFFFFFFF;
            }
        }
        else if (_isFd)
        {
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
            // 保持 USB 和旧 PCI/PCIe 的 Classic BTR0/BTR1 路径；PCI50xxU 使用 SDK 属性。
            initConfig.can.acc_code = 0;
            initConfig.can.acc_mask = 0xFFFFFFFF;
            initConfig.can.filter = 0;
            initConfig.can.mode = 0;
            if (driverClassicBitRate)
                SetRequiredProperty(channelIndex, "baud_rate", config.BaudRate.ToString());
            else
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

        if (_isFd || isPcieFd)
        {
            config.AppliedArbitrationBitRate = config.BaudRate;
            config.AppliedArbitrationSamplePoint = 80;
            if (_isFd)
            {
                config.AppliedDataBitRate = config.DataBitRate;
                config.AppliedDataSamplePoint = dataSamplePoint;
            }
        }
        else if (driverClassicBitRate)
        {
            config.AppliedArbitrationBitRate = config.BaudRate;
            config.AppliedArbitrationSamplePoint = null;
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

    private void SetRequiredProperty(uint channelIndex, string name, string value)
    {
        string path = $"{channelIndex}/{name}";
        if (!ZlgApi.TrySetProperty(_deviceHandle, path, value))
            throw new InvalidOperationException($"设置 ZLG 属性 {path}={value} 失败，请检查板卡型号、x64 二次开发库及驱动版本。");
    }

    private void CleanupFailedOpen()
    {
        if (_channelHandle != IntPtr.Zero)
        {
            try { ZlgApi.ResetCAN(_channelHandle); } catch { }
            _channelHandle = IntPtr.Zero;
        }
        if (_deviceHandle != IntPtr.Zero)
        {
            try { ZlgApi.CloseDevice(_deviceHandle); } catch { }
            _deviceHandle = IntPtr.Zero;
        }
        _isConnected = false;
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

        var deadline = timeoutMs < 0 ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var session = _diagnostics.BeginRead(filterId, timeoutMs, _isFd);

        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            int remainMs = (int)Math.Min((deadline - DateTime.UtcNow).TotalMilliseconds, 100);
            if (remainMs <= 0) break;

            CanMessage? msg = _isFd ? ReadOneFd(Math.Min(remainMs, 100)) : ReadOneClassic(Math.Min(remainMs, 100));
            if (msg != null)
            {
                if (filterId == null || msg.Id == filterId.Value)
                {
                    session.Matched();
                    return msg;
                }

                session.Filtered(msg.Id); // ID 不匹配，继续读取
            }
        }

        session.TimedOut();
        return null;
    }

    public string GetReceiveDiagnostics() => _diagnostics.Get();

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
