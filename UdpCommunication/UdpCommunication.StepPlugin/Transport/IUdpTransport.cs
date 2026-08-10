using System.Net;

namespace UdpCommunication.Transport;

public interface IUdpTransport : IDisposable
{
    IPEndPoint LocalEndPoint { get; }

    Task SendAsync(
        ReadOnlyMemory<byte> request,
        IPEndPoint remoteEndpoint,
        CancellationToken cancellationToken);

    Task<UdpTransportResult> ReceiveAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<UdpTransportResult> SendAndReceiveAsync(
        ReadOnlyMemory<byte> request,
        IPEndPoint remoteEndpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
