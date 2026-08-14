using System.Text.Json.Serialization;

namespace LIN.Models;

/// <summary>LIN 硬件适配器类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinAdapterType
{
    NI = 0,
    PEAK = 1,
    Vector = 2,
    IXXAT = 3
}

/// <summary>LIN 协议版本</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinVersionType
{
    /// <summary>LIN 1.x（最大 8 字节，经典校验）</summary>
    LIN_1x = 0,
    /// <summary>LIN 2.x（最大 8 字节，增强校验）</summary>
    LIN_2x = 1
}

/// <summary>LIN 帧校验类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinChecksumType
{
    /// <summary>经典校验（仅数据字节）</summary>
    Classic = 0,
    /// <summary>增强校验（保护 ID + 数据字节，LIN 2.x 推荐）</summary>
    Enhanced = 1
}

/// <summary>LIN 唤醒模式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinWakeupMode
{
    /// <summary>远程唤醒（在总线上发送唤醒模式，唤醒所有节点）</summary>
    Remote = 0,
    /// <summary>本地唤醒（仅唤醒本地接口，不发送总线唤醒模式）</summary>
    Local = 1
}

/// <summary>LIN 睡眠模式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinSleepMode
{
    /// <summary>远程睡眠（主节点发送 Go-to-Sleep 命令，全总线节点入睡）</summary>
    Remote = 0,
    /// <summary>本地睡眠（仅本地接口置为睡眠态，不发送总线信号）</summary>
    Local = 1
}
