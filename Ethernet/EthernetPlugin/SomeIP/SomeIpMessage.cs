using System.Buffers.Binary;
using Ethernet.SomeIP.Models;

namespace Ethernet.SomeIP;

/// <summary>SOME/IP 报文（16 字节头 + 负载）编解码。</summary>
public sealed class SomeIpMessage
{
    public const byte ProtocolVersionValue = 0x01;
    public const int HeaderLength = 16;

    public ushort ServiceId { get; set; }
    public ushort MethodId { get; set; }
    public ushort ClientId { get; set; }
    public ushort SessionId { get; set; }
    public byte ProtocolVersion { get; set; } = ProtocolVersionValue;
    public byte InterfaceVersion { get; set; } = 0x01;
    public SomeIpMessageType MessageType { get; set; } = SomeIpMessageType.Request;
    public byte ReturnCode { get; set; }
    public byte[] Payload { get; set; } = [];

    /// <summary>编码为完整报文字节数组。</summary>
    public byte[] Encode()
    {
        var buf = new byte[HeaderLength + Payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0), ServiceId);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(2), MethodId);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(4), (uint)(8 + Payload.Length));
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(8), ClientId);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(10), SessionId);
        buf[12] = ProtocolVersion;
        buf[13] = InterfaceVersion;
        buf[14] = (byte)MessageType;
        buf[15] = ReturnCode;
        Payload.CopyTo(buf, HeaderLength);
        return buf;
    }

    /// <summary>从字节数组解析报文，格式非法时返回 null。</summary>
    public static SomeIpMessage? TryDecode(byte[] data)
    {
        if (data.Length < HeaderLength) return null;
        var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4));
        if (length < 8 || data.Length < HeaderLength + (int)length - 8) return null;

        var payloadLen = (int)length - 8;
        return new SomeIpMessage
        {
            ServiceId        = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(0)),
            MethodId         = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2)),
            ClientId         = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8)),
            SessionId        = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10)),
            ProtocolVersion  = data[12],
            InterfaceVersion = data[13],
            MessageType      = (SomeIpMessageType)data[14],
            ReturnCode       = data[15],
            Payload          = data.AsSpan(HeaderLength, payloadLen).ToArray(),
        };
    }
}
