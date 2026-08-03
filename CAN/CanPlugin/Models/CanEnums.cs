namespace CAN.Models;

/// <summary>CAN 协议类型</summary>
public enum CanProtocolType
{
    /// <summary>经典 CAN 2.0（最大 8 字节）</summary>
    Classic = 0,
    /// <summary>CAN FD（最大 64 字节）</summary>
    FD = 1,
    /// <summary>CAN XL（最大 2048 字节）</summary>
    XL = 2
}

/// <summary>CAN 硬件适配器类型</summary>
public enum CanAdapterType
{
    NI = 0,
    PEAK = 1,
    Vector = 2,
    ZLG = 3
}

/// <summary>CAN 帧类型</summary>
public enum CanFrameType
{
    /// <summary>标准帧 (11-bit ID)</summary>
    Standard = 0,
    /// <summary>扩展帧 (29-bit ID)</summary>
    Extended = 1
}
