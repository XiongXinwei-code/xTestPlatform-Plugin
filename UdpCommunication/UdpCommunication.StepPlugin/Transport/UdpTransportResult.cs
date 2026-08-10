using System.Net;

namespace UdpCommunication.Transport;

public sealed record UdpTransportResult(byte[] Payload, IPEndPoint RemoteEndPoint);
