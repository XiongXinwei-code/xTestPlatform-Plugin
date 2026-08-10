using System.Text.Json.Serialization;

namespace Ethernet;

/// <summary>Socket 数据编码格式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EthernetDataEncoding
{
    /// <summary>十六进制字符串（如 "01 02 03"）</summary>
    Hex = 0,
    /// <summary>UTF-8 文本</summary>
    Utf8 = 1,
    /// <summary>ASCII 文本</summary>
    Ascii = 2
}

/// <summary>UDP 接收地址绑定模式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UdpBindMode
{
    /// <summary>绑定到本机指定端口</summary>
    LocalPort = 0,
    /// <summary>绑定到所有网卡（0.0.0.0）</summary>
    AnyInterface = 1
}
