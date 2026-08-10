namespace UdpCommunication.Transport;

public sealed record UdpEndpointOptions(string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort);
