using System.Net;

namespace UdpCommunication.StepPlugin.Transport;

public sealed record UdpTransportResult(byte[] Payload, IPEndPoint RemoteEndPoint);
