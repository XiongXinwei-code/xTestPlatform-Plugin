using System.Text.Json.Serialization;

namespace UdpCommunication.Protocol;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UdpReplyMatchMode
{
    Exact,
    Contains
}
