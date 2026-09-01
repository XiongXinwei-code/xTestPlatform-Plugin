using CAN.Models;

namespace CAN.Adapters.Tosun;

/// <summary>同星 TOSUN CAN 适配器实现（TSCAN API）</summary>
public sealed class TosunAdapter : ICanAdapter
{
    private nuint _deviceHandle;
    private int _channelIndex;
    private bool _isConnected;
    private bool _isFd;
    private bool _libInitialized;

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
                "未找到 libTSCAN.dll，请安装同星 TSMaster 或 TSCAN API 运行库，并将 DLL 所在目录加入 PATH。" +
                "下载地址: https://www.tosunai.com/");
        }
    }

    private void OpenInternal(CanAdapterConfig config)
    {
        // Channel 参数为通道索引（0、1、2…）
        if (!int.TryParse(config.Channel.Trim(), out _channelIndex) || _channelIndex < 0)
            throw new ArgumentException($"无效的 TOSUN 通道 '{config.Channel}'，应为通道索引（0、1、2…）");

        if (Math.Abs(config.ArbitrationSamplePoint - 80d) > 0.01)
        {
            throw new InvalidOperationException(
                $"当前插件使用的 libTSCAN tscan_config_*_by_baudrate 接口没有采样点参数，" +
                $"不能可靠表达 {config.ArbitrationSamplePoint:F2}%；请设为 80%，或改用提供寄存器配置接口的 TSMaster SDK。");
        }

        _isFd = config.Protocol == CanProtocolType.FD;

        TosunApi.InitializeLib(true, false); // 启用 FIFO 接收模式
        _libInitialized = true;

        // 连接第一个 USB 设备（ip 传空字符串表示 USB）
        var status = TosunApi.Connect("", ref _deviceHandle);
        if (status != TosunApi.TSCAN_OK)
        {
            TosunApi.FinalizeLib();
            _libInitialized = false;
            throw new InvalidOperationException($"连接 TOSUN 设备失败（错误码 {status}），请检查设备连接");
        }

        if (_isFd)
        {
            // 波特率单位为 kbps；控制器类型 1=ISO CANFD，模式 0=正常
            TosunApi.CheckStatus(TosunApi.ConfigCanFdByBaudrate(
                _deviceHandle, _channelIndex,
                config.BaudRate / 1000.0, config.DataBitRate / 1000.0,
                1, 0, config.EnableTermination ? 1u : 0u), "配置 CANFD 波特率");
        }
        else
        {
            TosunApi.CheckStatus(TosunApi.ConfigCanByBaudrate(
                _deviceHandle, _channelIndex, config.BaudRate / 1000.0,
                config.EnableTermination ? 1u : 0u), "配置 CAN 波特率");
        }

        config.AppliedArbitrationBitRate = config.BaudRate;
        config.AppliedArbitrationSamplePoint = 80;

        _isConnected = true;
    }

    public void Close()
    {
        if (_isConnected)
        {
            TosunApi.Disconnect(_deviceHandle);
            _deviceHandle = 0;
            _isConnected = false;
        }
        if (_libInitialized)
        {
            TosunApi.FinalizeLib();
            _libInitialized = false;
        }
    }

    public void Write(CanMessage message)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        byte properties = TosunApi.PROP_TX;
        if (message.FrameType == CanFrameType.Extended)
            properties |= TosunApi.PROP_EXTENDED_DATA;

        if (_isFd && message.IsFd)
        {
            var msg = new TosunApi.TLibCANFD
            {
                FIdxChn = (byte)_channelIndex,
                FProperties = properties,
                FDLC = LengthToDlc(message.Data.Length),
                FFDProperties = TosunApi.FD_PROP_BRS,
                FIdentifier = (int)message.Id,
                FData = new byte[64]
            };
            Array.Copy(message.Data, msg.FData, Math.Min(message.Data.Length, 64));
            TosunApi.CheckStatus(TosunApi.TransmitCanFdSync(_deviceHandle, ref msg, 1000), "发送 CANFD 报文");
        }
        else
        {
            var msg = new TosunApi.TLibCAN
            {
                FIdxChn = (byte)_channelIndex,
                FProperties = properties,
                FDLC = (byte)Math.Min(message.Data.Length, 8),
                FIdentifier = (int)message.Id,
                FData = new byte[8]
            };
            Array.Copy(message.Data, msg.FData, Math.Min(message.Data.Length, 8));
            TosunApi.CheckStatus(TosunApi.TransmitCanSync(_deviceHandle, ref msg, 1000), "发送 CAN 报文");
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
            Thread.Sleep(1); // 接收 FIFO 为空，短暂等待
        }
        return null;
    }

    private CanMessage? ReadOneClassic()
    {
        var buffer = new TosunApi.TLibCAN[1];
        int size = 1;
        var status = TosunApi.ReceiveCanMsgs(_deviceHandle, buffer, ref size, _channelIndex, 0);
        if (status != TosunApi.TSCAN_OK || size == 0) return null;

        var m = buffer[0];
        int len = Math.Min(m.FDLC, (byte)8);
        var data = new byte[len];
        Array.Copy(m.FData, data, len);
        return new CanMessage
        {
            Id = (uint)m.FIdentifier,
            FrameType = (m.FProperties & TosunApi.PROP_EXTENDED_DATA) != 0 ? CanFrameType.Extended : CanFrameType.Standard,
            Data = data,
            TimestampNs = (long)m.FTimeUs * 1_000
        };
    }

    private CanMessage? ReadOneFd()
    {
        var buffer = new TosunApi.TLibCANFD[1];
        int size = 1;
        var status = TosunApi.ReceiveCanFdMsgs(_deviceHandle, buffer, ref size, _channelIndex, 0);
        if (status != TosunApi.TSCAN_OK || size == 0) return null;

        var m = buffer[0];
        int len = DlcToLength(m.FDLC);
        var data = new byte[len];
        Array.Copy(m.FData, data, len);
        return new CanMessage
        {
            Id = (uint)m.FIdentifier,
            FrameType = (m.FProperties & TosunApi.PROP_EXTENDED_DATA) != 0 ? CanFrameType.Extended : CanFrameType.Standard,
            Data = data,
            IsFd = true,
            TimestampNs = (long)m.FTimeUs * 1_000
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
