using System.Text.Json.Serialization;

namespace UdpCommunication.Protocol;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UdpPacketFormat
{
    Utf8Text,
    Hexadecimal
}
