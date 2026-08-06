using System.Text.Json.Serialization;

namespace Ethernet.DoIP.Models;

/// <summary>路由激活类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DoipActivationType
{
    /// <summary>默认激活（0x00）</summary>
    Default,
    /// <summary>WWH-OBD 激活（0x01）</summary>
    WwhObd,
    /// <summary>中央安全激活（0xE0）</summary>
    CentralSecurity
}
