using System.Text.Json.Serialization;

namespace CAN.XCP;

/// <summary>XCP 连接模式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum XcpConnectMode
{
    /// <summary>普通连接</summary>
    Normal = 0,
    /// <summary>用户自定义模式</summary>
    UserDefined = 1
}

/// <summary>XCP 地址扩展</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum XcpAddressExtension
{
    /// <summary>无扩展</summary>
    None = 0,
    /// <summary>ODT 扩展</summary>
    Odt = 1,
    /// <summary>DAQ 扩展</summary>
    Daq = 2
}

/// <summary>XCP 字节序</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum XcpByteOrder
{
    /// <summary>小端（Intel 格式）</summary>
    LittleEndian = 0,
    /// <summary>大端（Motorola 格式）</summary>
    BigEndian = 1
}
