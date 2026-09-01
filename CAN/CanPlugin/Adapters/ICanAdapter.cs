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

/// <summary>为协议层提供适配器最近一次接收过程的诊断信息。</summary>
public interface ICanAdapterDiagnostics
{
    /// <summary>返回最近一次 Read 调用的接收统计，供超时错误定位使用。</summary>
    string GetReceiveDiagnostics();
}

/// <summary>CAN 适配器配置</summary>
public class CanAdapterConfig
{
    public string Channel { get; set; } = "";
    public int BaudRate { get; set; } = 500_000;
    public CanProtocolType Protocol { get; set; } = CanProtocolType.Classic;
    public int DataBitRate { get; set; } = 2_000_000;
    /// <summary>是否使能硬件内置终端电阻；由支持该能力的适配器处理。</summary>
    public bool EnableTermination { get; set; }
    /// <summary>仲裁段目标采样点百分比；底层位时序由适配器换算。</summary>
    public double ArbitrationSamplePoint { get; set; } = 80.0;
    /// <summary>CAN FD 数据段目标采样点百分比；经典 CAN 忽略。</summary>
    public double DataSamplePoint { get; set; } = 80.0;
    /// <summary>适配器成功初始化后回填的实际仲裁段波特率。</summary>
    public double? AppliedArbitrationBitRate { get; set; }
    /// <summary>适配器成功初始化后回填的实际仲裁段采样点。</summary>
    public double? AppliedArbitrationSamplePoint { get; set; }
    /// <summary>仲裁段自定义位时序；null 表示使用驱动根据 BaudRate 选择的默认时序。</summary>
    public CanBitTimingConfig? ArbitrationBitTiming { get; set; }
    /// <summary>CAN FD 数据段自定义位时序；null 表示使用驱动默认时序。</summary>
    public CanBitTimingConfig? DataBitTiming { get; set; }
    /// <summary>适配器成功初始化后回填的实际数据段波特率。</summary>
    public double? AppliedDataBitRate { get; set; }
    /// <summary>适配器成功初始化后回填的实际数据段采样点。</summary>
    public double? AppliedDataSamplePoint { get; set; }
    public string DatabasePath { get; set; } = "";
    /// <summary>接收缓冲区大小（帧数）</summary>
    public int RxQueueSize { get; set; } = 8192;
}

/// <summary>已经解析并验证的 CAN 仲裁段位时序。</summary>
public sealed class CanBitTimingConfig
{
    public int Brp { get; init; }
    public int Sjw { get; init; }
    public int Tseg1 { get; init; }
    public int Tseg2 { get; init; }
    public double ActualBitRate { get; init; }
    public double SamplePoint { get; init; }
    public ulong NiXnetBaudRate64 { get; init; }
}
