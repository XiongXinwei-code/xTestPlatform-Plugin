namespace CAN.Models;

/// <summary>统一 CAN 报文结构，支持 Classic/FD</summary>
public class CanMessage
{
    /// <summary>CAN ID（标准帧 11-bit，扩展帧 29-bit）</summary>
    public uint Id { get; set; }

    /// <summary>帧类型（标准/扩展）</summary>
    public CanFrameType FrameType { get; set; } = CanFrameType.Standard;

    /// <summary>数据载荷</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>是否为 CAN FD 帧（BRS 位速率切换）</summary>
    public bool IsFd { get; set; }

    /// <summary>接收时间戳（纳秒）</summary>
    public long TimestampNs { get; set; }
    /// <summary>NI 原始 FILETIME 时间戳（100 ns）；不以出队时刻替代。</summary>
    public ulong? RawTimestamp100ns { get; set; }
    /// <summary>ZLG 原始设备时间戳（微秒）。不转换成 NI FILETIME。</summary>
    public ulong? RawTimestampUs { get; set; }

    /// <summary>时间域未经厂商契约核实时保持 Unknown。</summary>
    public string TimestampDomain { get; set; } = "Unknown";
    public string ClockId { get; set; } = "Unknown";
    /// <summary>最早托管观察时刻；不等价于总线采集时刻。</summary>
    public long? IngressMonotonicTicks { get; set; }
    /// <summary>仅可信硬件时钟映射可填入的采集时刻下界，用于保守帧龄。</summary>
    public long? CaptureMonotonicLowerBoundTicks { get; set; }

    /// <summary>是否为厂商发送回显，不进入普通 CANopen 反馈路由。</summary>
    public bool IsTransmitEcho { get; set; }

    /// <summary>数据长度码 DLC</summary>
    public int Dlc => Data.Length;
}
