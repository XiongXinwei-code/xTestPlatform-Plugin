using CAN.Models;

namespace CAN.Adapters;

/// <summary>CAN 硬件适配器统一抽象接口</summary>
public interface ICanAdapter : IDisposable
{
    /// <summary>打开 CAN 通道</summary>
    void Open(CanAdapterConfig config);

    /// <summary>关闭 CAN 通道</summary>
    void Close();

    /// <summary>发送 CAN 报文</summary>
    void Write(CanMessage message);

    /// <summary>接收 CAN 报文（阻塞直到超时）</summary>
    CanMessage? Read(int timeoutMs, CancellationToken ct = default);

    /// <summary>接收指定 ID 的 CAN 报文</summary>
    CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default);

    /// <summary>是否已连接</summary>
    bool IsConnected { get; }
}

/// <summary>CAN 适配器配置</summary>
public class CanAdapterConfig
{
    public string Channel { get; set; } = "";
    public int BaudRate { get; set; } = 500_000;
    public CanProtocolType Protocol { get; set; } = CanProtocolType.Classic;
    public int DataBitRate { get; set; } = 2_000_000;
    public string DatabasePath { get; set; } = "";
}
