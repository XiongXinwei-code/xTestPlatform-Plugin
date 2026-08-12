using CAN.Models;

namespace CAN.Adapters.Kvaser;

/// <summary>Kvaser CAN 适配器实现（CANlib）</summary>
public sealed class KvaserAdapter : ICanAdapter
{
    private int _handle = -1;
    private bool _isConnected;
    private bool _isFd;

    public bool IsConnected => _isConnected;

    public void Open(CanAdapterConfig config)
    {
        if (_isConnected) throw new InvalidOperationException("CAN 通道已打开");
        try
        {
            OpenInternal(config);
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "未找到 canlib32.dll，请安装 Kvaser 驱动（Kvaser Drivers for Windows）。" +
                "下载地址: https://kvaser.com/download/");
        }
    }

    private void OpenInternal(CanAdapterConfig config)
    {
        // Channel 参数为 CANlib 通道索引（0、1、2…），在 Kvaser Hardware 工具中查看
        if (!int.TryParse(config.Channel.Trim(), out int channelIndex) || channelIndex < 0)
            throw new ArgumentException($"无效的 Kvaser 通道 '{config.Channel}'，应为通道索引（0、1、2…）");

        _isFd = config.Protocol == CanProtocolType.FD;

        KvaserApi.InitializeLibrary();

        int flags = KvaserApi.canOPEN_ACCEPT_VIRTUAL;
        if (_isFd) flags |= KvaserApi.canOPEN_CAN_FD;

        _handle = KvaserApi.OpenChannel(channelIndex, flags);
        if (_handle < 0)
            KvaserApi.CheckStatus(_handle); // 句柄为负即错误码

        // 仲裁段波特率（使用预定义常量，tseg/sjw 置 0 表示采用默认时序）
        KvaserApi.CheckStatus(KvaserApi.SetBusParams(_handle, KvaserApi.ToBitrateConst(config.BaudRate), 0, 0, 0, 0, 0));

        // FD 数据段波特率
        if (_isFd)
            KvaserApi.CheckStatus(KvaserApi.SetBusParamsFd(_handle, KvaserApi.ToFdDataBitrateConst(config.DataBitRate), 0, 0, 0));

        KvaserApi.CheckStatus(KvaserApi.BusOn(_handle));
        _isConnected = true;
    }

    public void Close()
    {
        if (!_isConnected) return;
        KvaserApi.BusOff(_handle);
        KvaserApi.Close(_handle);
        _handle = -1;
        _isConnected = false;
    }

    public void Write(CanMessage message)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        uint flag = message.FrameType == CanFrameType.Extended
            ? KvaserApi.canMSG_EXT
            : KvaserApi.canMSG_STD;

        int maxLen = 8;
        if (_isFd && message.IsFd)
        {
            flag |= KvaserApi.canFDMSG_FDF | KvaserApi.canFDMSG_BRS;
            maxLen = 64;
        }

        int len = Math.Min(message.Data.Length, maxLen);
        var buffer = new byte[len];
        Array.Copy(message.Data, buffer, len);

        KvaserApi.CheckStatus(KvaserApi.WriteWait(_handle, (int)message.Id, buffer, (uint)len, flag, 1000));
    }

    public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => ReadInternal(null, timeoutMs, ct);

    public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => ReadInternal(id, timeoutMs, ct);

    private CanMessage? ReadInternal(uint? filterId, int timeoutMs, CancellationToken ct)
    {
        if (!_isConnected) throw new InvalidOperationException("CAN 通道未打开");

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var buffer = new byte[64];

        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            int remainMs = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remainMs <= 0) break;

            var status = KvaserApi.ReadWait(_handle, out int id, buffer, out uint dlc, out uint flag, out uint time, (uint)Math.Min(remainMs, 100));
            if (status == KvaserApi.canERR_NOMSG) continue;
            KvaserApi.CheckStatus(status);

            int len = (int)Math.Min(dlc, (uint)buffer.Length);
            var data = new byte[len];
            Array.Copy(buffer, data, len);

            var msg = new CanMessage
            {
                Id = (uint)id,
                FrameType = (flag & KvaserApi.canMSG_EXT) != 0 ? CanFrameType.Extended : CanFrameType.Standard,
                Data = data,
                IsFd = (flag & KvaserApi.canFDMSG_FDF) != 0,
                TimestampNs = (long)time * 1_000_000 // CANlib 默认毫秒
            };

            if (filterId == null || msg.Id == filterId.Value)
                return msg;
            // ID 不匹配，继续读取
        }
        return null;
    }

    public void Dispose() => Close();
}
