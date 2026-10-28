using CAN.Models;
using CAN.Runtime;

namespace CAN.Adapters.Tosun;

/// <summary>同星 TOSUN CAN 适配器实现（TSCAN API）</summary>
public sealed class TosunAdapter : ICanAdapter, ICanAdapterDiagnostics, ICanCloseState
{
    // libTSCAN 初始化/终止为全局库操作，disconnect 为设备级操作。
    // 在未实现厂商证实的共享设备管理前，禁止不同 adapter 并行 Init/Finalize。
    private static TosunAdapter? _libraryOwner;
    private readonly object _lifecycleGate = new();
    private bool _ownsLibrary;
    private string? _ownershipConflict;
    private nuint _deviceHandle;
    private int _channelIndex;
    private bool _isConnected;
    private bool _isFd;
    private readonly CanReceiveDiagnostics _diagnostics = new("TOSUN");
    private bool _libInitialized;
    private readonly Func<nuint, uint> _disconnect;
    private readonly Action _finalize;
    private readonly TosunIo _io;
    public TosunAdapter() : this(new TosunIo()) { }
    internal TosunAdapter(TosunIo io) { _io = io; _disconnect = io.Disconnect; _finalize = io.FinalizeLibrary; }
    internal TosunAdapter(Func<nuint, uint> disconnect, Action finalize) : this(new TosunIo()) => (_disconnect, _finalize) = (disconnect, finalize);

    public bool IsConnected => _isConnected;
    public bool HandlesReleased => _deviceHandle == 0 && !_libInitialized && !_ownsLibrary;

    public void Open(CanAdapterConfig config)
    {
        lock (_lifecycleGate)
        {
            if (_ownsLibrary || _deviceHandle != 0 || _libInitialized)
                throw new InvalidOperationException("此 TOSUN 适配器已打开或尚未确认退出，请先关闭原实例");
            try { OpenInternal(config); }
            catch (Exception openError)
            {
                // 只清理本实例已确认取得的资源；未知归属句柄保持隔离，不调用 Disconnect/Finalize。
                try { Close(); }
                catch (Exception closeError)
                { throw new AggregateException(_ownershipConflict ?? "TOSUN 打开失败且释放尚未确认；保留库所有权，请重试关闭", openError, closeError); }
                if (openError is DllNotFoundException)
                    throw new InvalidOperationException(
                        "未找到 libTSCAN.dll，请安装同星 TSMaster 或 TSCAN API 运行库，并将 DLL 所在目录加入 PATH。" +
                        "下载地址: https://www.tosunai.com/", openError);
                throw;
            }
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
        if (config.Protocol == CanProtocolType.FD && Math.Abs(config.DataSamplePoint - 80d) > 0.01)
        {
            throw new InvalidOperationException(
                $"当前 libTSCAN CAN FD 波特率接口没有数据段采样点参数，不能可靠表达 {config.DataSamplePoint:F2}%；" +
                "请设为 80%，或改用提供寄存器配置接口的 TSMaster SDK。");
        }

        _isFd = config.Protocol == CanProtocolType.FD;

        if (Interlocked.CompareExchange(ref _libraryOwner, this, null) != null)
            throw new InvalidOperationException("当前插件仅支持一个 TOSUN 库所有者；其他 TOSUN 连接仍在运行或退出中，请先确认关闭，不能并行打开不同通道/设备");
        _ownsLibrary = true;
        // 初始化抛出非加载错误时，不能假定库没有取得资源，保留配对 Finalize 的责任。
        _libInitialized = true;
        try { _io.Initialize(true, false, false); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { _libInitialized = false; throw; }

        // 连接第一个 USB 设备（ip 传空字符串表示 USB）
        nuint connectedHandle = 0;
        uint status;
        try { status = _io.Connect("", ref connectedHandle); }
        catch
        {
            if (connectedHandle != 0) RecordOwnershipConflict("Connect 抛出异常", connectedHandle);
            throw;
        }
        if (status != TosunApi.TSCAN_OK)
        {
            // 官方 status=5 表示设备已经连接。非零值本身不是本实例取得所有权的证明。
            if (status == 5 || connectedHandle != 0) RecordOwnershipConflict($"Connect 返回 {status}", connectedHandle);
            throw new InvalidOperationException($"连接 TOSUN 设备失败（错误码 {status}），请检查设备连接");
        }
        if (connectedHandle == 0) throw new InvalidOperationException("TOSUN Connect 成功但未返回有效句柄");
        _deviceHandle = connectedHandle;

        if (_isFd)
        {
            // 波特率单位为 kbps；控制器类型 1=ISO CANFD，模式 0=正常
            TosunApi.CheckStatus(_io.ConfigureFd(
                _deviceHandle, _channelIndex,
                config.BaudRate / 1000.0, config.DataBitRate / 1000.0,
                config.EnableTermination ? 1u : 0u), "配置 CANFD 波特率");
        }
        else
        {
            TosunApi.CheckStatus(_io.ConfigureCan(
                _deviceHandle, _channelIndex, config.BaudRate / 1000.0,
                config.EnableTermination ? 1u : 0u), "配置 CAN 波特率");
        }

        config.AppliedArbitrationBitRate = config.BaudRate;
        config.AppliedArbitrationSamplePoint = 80;
        if (_isFd)
        {
            config.AppliedDataBitRate = config.DataBitRate;
            config.AppliedDataSamplePoint = 80;
        }

        _isConnected = true;
    }

    public void Close()
    {
        lock (_lifecycleGate)
        {
            _isConnected = false;
            if (_ownershipConflict != null) throw new InvalidOperationException(_ownershipConflict);
            if (_deviceHandle != 0)
            {
                TosunApi.CheckStatus(_disconnect(_deviceHandle), "断开设备（失败保留句柄与库租约，可重试关闭）");
                _deviceHandle = 0;
            }
            if (_libInitialized)
            {
                _finalize();
                _libInitialized = false;
            }
            if (_ownsLibrary)
            {
                Interlocked.CompareExchange(ref _libraryOwner, null, this);
                _ownsLibrary = false;
            }
        }
    }

    private void RecordOwnershipConflict(string reason, nuint handle) => _ownershipConflict =
        $"TOSUN 库所有权冲突：{reason}，返回句柄=0x{handle:X}，未证明归属于本连接。" +
        "为避免干预已有设备，不执行 Disconnect/Finalize；库保持隔离。请关闭同进程其他 libTSCAN 使用者后重启宿主，再由本插件独占库生命周期。";

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

        var deadline = timeoutMs < 0 ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var session = _diagnostics.BeginRead(filterId, timeoutMs, _isFd);

        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            CanMessage? msg = _isFd ? ReadOneFd() : ReadOneClassic();
            if (msg != null)
            {
                if (filterId == null || msg.Id == filterId.Value)
                {
                    session.Matched();
                    return msg;
                }

                session.Filtered(msg.Id);
                continue; // ID 不匹配，继续读取
            }
            Thread.Sleep(1); // 接收 FIFO 为空，短暂等待
        }

        session.TimedOut();
        return null;
    }

    public string GetReceiveDiagnostics() => _diagnostics.Get();

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
