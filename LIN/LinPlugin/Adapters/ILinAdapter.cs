using LIN.Models;

namespace LIN.Adapters;

/// <summary>LIN 硬件适配器统一抽象接口</summary>
public interface ILinAdapter : IDisposable
{
    /// <summary>打开 LIN 通道</summary>
    void Open(LinAdapterConfig config);

    /// <summary>关闭 LIN 通道</summary>
    void Close();

    /// <summary>发送 LIN 帧（主节点发送帧头，提供数据）</summary>
    void Write(LinFrame frame);

    /// <summary>接收 LIN 帧（阻塞直到超时）</summary>
    LinFrame? Read(int timeoutMs, CancellationToken ct = default);

    /// <summary>接收指定 ID 的 LIN 帧</summary>
    LinFrame? Read(byte frameId, int timeoutMs, CancellationToken ct = default);

    /// <summary>唤醒 LIN 总线（remote 为 true 时在总线上发送唤醒模式，否则仅唤醒本地接口）</summary>
    void Wakeup(bool remote = true);

    /// <summary>使 LIN 总线进入睡眠（remote 为 true 时由主节点发送 Go-to-Sleep 命令，否则仅本地接口睡眠）</summary>
    void Sleep(bool remote = true);

    /// <summary>是否已连接</summary>
    bool IsConnected { get; }
}

/// <summary>LIN 适配器配置</summary>
public class LinAdapterConfig
{
    public string Channel { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 19200;
    public LinVersionType LinVersion { get; set; } = LinVersionType.LIN_2x;
    public bool IsMaster { get; set; } = true;
    /// <summary>接收缓冲区大小（帧数）</summary>
    public int RxQueueSize { get; set; } = 512;
}
