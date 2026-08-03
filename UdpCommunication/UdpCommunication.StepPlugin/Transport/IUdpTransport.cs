namespace UdpCommunication.StepPlugin.Transport;

public interface IUdpTransport
{
    Task SendAsync(
        UdpEndpointOptions endpoint,
        ReadOnlyMemory<byte> request,
        CancellationToken cancellationToken);

    Task<UdpTransportResult> SendAndReceiveAsync(
        UdpEndpointOptions endpoint,
        ReadOnlyMemory<byte> request,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
