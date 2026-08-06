using LIN.Helpers;
using LIN.Models;

namespace LIN.Adapters.NiXnet;

/// <summary>NI-XNET LIN 适配器实现（基于 nixnet.dll P/Invoke）</summary>
public sealed class NiXnetLinAdapter : ILinAdapter
{
    private uint _rxSession;
    private uint _txSession;
    private bool _isConnected;
    private readonly object _lock = new();

    public bool IsConnected => _isConnected;

    public void Open(LinAdapterConfig config)
    {
        if (_isConnected) throw new InvalidOperationException("LIN 通道已打开");
        try
        {
            OpenInternal(config);
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "未找到 nixnet.dll，请安装 NI-XNET 驱动程序。" +
                "下载地址: https://www.ni.com/zh-cn/support/downloads/drivers/download.ni-xnet.html");
        }
    }

    private void OpenInternal(LinAdapterConfig config)
    {
        var interfaceName = config.Channel;

        // 创建接收会话（Frame In Stream）
        var status = NiXnetLinApi.nxCreateSession(
            "", "", "", interfaceName,
            NiXnetLinApi.nxMode_FrameInStream,
            out _rxSession);
        NiXnetLinApi.CheckStatus(status);

        // 创建发送会话（Frame Out Stream）
        status = NiXnetLinApi.nxCreateSession(
            "", "", "", interfaceName,
            NiXnetLinApi.nxMode_FrameOutStream,
            out _txSession);
        NiXnetLinApi.CheckStatus(status);

        // 设置波特率
        uint baudRate = (uint)config.BaudRate;
        status = NiXnetLinApi.nxSetProperty(_rxSession,
            NiXnetLinApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
        NiXnetLinApi.CheckStatus(status);

        status = NiXnetLinApi.nxSetProperty(_txSession,
            NiXnetLinApi.nxPropSession_IntfBaudRate, 4, ref baudRate);
        NiXnetLinApi.CheckStatus(status);

        // 启动会话
        status = NiXnetLinApi.nxStart(_rxSession, NiXnetLinApi.nxStartStop_SessionOnly);
        NiXnetLinApi.CheckStatus(status);

        status = NiXnetLinApi.nxStart(_txSession, NiXnetLinApi.nxStartStop_SessionOnly);
        NiXnetLinApi.CheckStatus(status);

        _isConnected = true;
    }

    public void Close()
    {
        lock (_lock)
        {
            if (!_isConnected) return;
            try
            {
                if (_rxSession != 0) { NiXnetLinApi.nxStop(_rxSession, NiXnetLinApi.nxStartStop_SessionOnly); NiXnetLinApi.nxClear(_rxSession); }
                if (_txSession != 0) { NiXnetLinApi.nxStop(_txSession, NiXnetLinApi.nxStartStop_SessionOnly); NiXnetLinApi.nxClear(_txSession); }
            }
            finally
            {
                _rxSession = 0;
                _txSession = 0;
                _isConnected = false;
            }
        }
    }

    public void Write(LinFrame frame)
    {
        if (!_isConnected) throw new InvalidOperationException("LIN 通道未打开");

        // 构造 NI-XNET LIN 帧字节流并写入
        // 帧格式: [FrameId(1), DataLen(1), Data(N)]
        var buf = new byte[2 + frame.Data.Length];
        buf[0] = LinHelper.CalcProtectedId(frame.FrameId);
        buf[1] = (byte)frame.Data.Length;
        Array.Copy(frame.Data, 0, buf, 2, frame.Data.Length);

        uint bytesWritten = 0;
        var status = NiXnetLinApi.nxWriteFrame(_txSession, buf, (uint)buf.Length, 0.1, out bytesWritten);
        NiXnetLinApi.CheckStatus(status);
    }

    public LinFrame? Read(int timeoutMs, CancellationToken ct = default)
        => ReadInternal(null, timeoutMs, ct);

    public LinFrame? Read(byte frameId, int timeoutMs, CancellationToken ct = default)
        => ReadInternal(frameId, timeoutMs, ct);

    private LinFrame? ReadInternal(byte? filterFrameId, int timeoutMs, CancellationToken ct)
    {
        if (!_isConnected) throw new InvalidOperationException("LIN 通道未打开");

        double timeoutSec = timeoutMs / 1000.0;
        var buf = new byte[512];
        uint bytesRead = 0;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var status = NiXnetLinApi.nxReadFrame(_rxSession, buf, (uint)buf.Length, timeoutSec, out bytesRead);
            if (status != 0 || bytesRead < 2) continue;

            byte rawId = buf[0];
            byte dataLen = buf[1];
            if (dataLen > 8 || bytesRead < (uint)(2 + dataLen)) continue;

            byte frameId = (byte)(rawId & 0x3F);
            if (filterFrameId.HasValue && frameId != filterFrameId.Value) continue;

            var data = new byte[dataLen];
            Array.Copy(buf, 2, data, 0, dataLen);

            return new LinFrame
            {
                FrameId = frameId,
                Data = data,
                TimestampNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L
            };
        }

        ct.ThrowIfCancellationRequested();
        return null;
    }

    public void Dispose() => Close();
}
