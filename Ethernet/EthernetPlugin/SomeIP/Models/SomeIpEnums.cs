using System.Text.Json.Serialization;

namespace Ethernet.SomeIP.Models;

/// <summary>SOME/IP 消息类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SomeIpMessageType : byte
{
    /// <summary>请求（期望响应）</summary>
    Request = 0x00,
    /// <summary>无响应请求</summary>
    RequestNoReturn = 0x01,
    /// <summary>通知（事件）</summary>
    Notification = 0x02,
    /// <summary>响应</summary>
    Response = 0x80,
    /// <summary>错误响应</summary>
    Error = 0x81,
}
