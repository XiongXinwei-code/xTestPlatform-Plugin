using CAN.Helpers;
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
        if (config.EnableTermination)
        {
            throw new InvalidOperationException(
                "当前 PCAN-Basic 通用接口不能控制 PEAK 设备内置终端电阻；请取消勾选并外接 120 Ω 电阻。");
        }

        _channel = PcanApi.ParseChannel(config.Channel);
        _isFd = config.Protocol == CanProtocolType.FD;

        if (_isFd)
        {
            var arbitration = CanSamplePointCalculator.Calculate(
                80_000_000, config.BaudRate, config.ArbitrationSamplePoint,
                maxPrescaler: 1024, maxTseg1: 256, maxTseg2: 128, maxSjw: 128, maxTotalTq: 385);
            var data = CanSamplePointCalculator.Calculate(
                80_000_000, config.DataBitRate, config.DataSamplePoint,
                maxPrescaler: 1024, maxTseg1: 32, maxTseg2: 16, maxSjw: 16, maxTotalTq: 49);

            // PCAN-Basic FD 使用 80 MHz 时钟的位速率字符串。
            var bitrateFd =
                $"f_clock_mhz=80,nom_brp={arbitration.Prescaler},nom_tseg1={arbitration.Tseg1},nom_tseg2={arbitration.Tseg2},nom_sjw={arbitration.Sjw}," +
                $"data_brp={data.Prescaler},data_tseg1={data.Tseg1},data_tseg2={data.Tseg2},data_sjw={data.Sjw}";
            PcanApi.CheckStatus(PcanApi.InitializeFD(_channel, bitrateFd));
            config.AppliedArbitrationBitRate = arbitration.ActualBitRate;
            config.AppliedArbitrationSamplePoint = arbitration.SamplePoint;
            config.AppliedDataBitRate = data.ActualBitRate;
            config.AppliedDataSamplePoint = data.SamplePoint;
        }
        else
        {
            var arbitration = CanSamplePointCalculator.CalculateSja1000(
                config.BaudRate, config.ArbitrationSamplePoint);
            PcanApi.CheckStatus(PcanApi.Initialize(
                _channel, CanSamplePointCalculator.ToSja1000Btr(arbitration), 0, 0, 0));
            config.AppliedArbitrationBitRate = arbitration.ActualBitRate;
            config.AppliedArbitrationSamplePoint = arbitration.SamplePoint;
        }

        _isConnected = true;
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
