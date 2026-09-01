using System.Text.Json.Serialization;

namespace CAN.Models;

/// <summary>CAN 协议类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanProtocolType
{
    /// <summary>经典 CAN 2.0（最大 8 字节）</summary>
    Classic = 0,
    /// <summary>CAN FD（最大 64 字节）</summary>
    FD = 1
}

/// <summary>CAN 硬件适配器类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanAdapterType
{
    NI = 0,
    PEAK = 1,
    Vector = 2,
    ZLG = 3,
    Kvaser = 4,
    TOSUN = 5
}

/// <summary>仲裁段位时序配置方式（当前由 NI-XNET 适配器实现）</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanBitTimingMode
{
    /// <summary>按波特率使用驱动默认位时序</summary>
    Automatic = 0,
    /// <summary>按目标采样点自动计算 BRP/SJW/TSEG1/TSEG2</summary>
    SamplePoint = 1,
    /// <summary>手动指定 BRP/SJW/TSEG1/TSEG2</summary>
    Registers = 2
}

/// <summary>CAN 帧类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanFrameType
{
    /// <summary>标准帧 (11-bit ID)</summary>
    Standard = 0,
    /// <summary>扩展帧 (29-bit ID)</summary>
    Extended = 1
}
