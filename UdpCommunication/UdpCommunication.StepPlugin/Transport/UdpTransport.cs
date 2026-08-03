using System.Net;
using System.Net.Sockets;

namespace UdpCommunication.StepPlugin.Transport;

public sealed class UdpTransport : IUdpTransport
{
    public async Task SendAsync(UdpEndpointOptions endpoint, ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var client = CreateClient(endpoint);
        await client.SendAsync(request, new IPEndPoint(IPAddress.Parse(endpoint.RemoteAddress), endpoint.RemotePort), cancellationToken);
    }

    public async Task<UdpTransportResult> SendAndReceiveAsync(UdpEndpointOptions endpoint, ReadOnlyMemory<byte> request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        using var client = CreateClient(endpoint);
        var remoteEndpoint = new IPEndPoint(IPAddress.Parse(endpoint.RemoteAddress), endpoint.RemotePort);
        await client.SendAsync(request, remoteEndpoint, cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            while (true)
            {
                var reply = await client.ReceiveAsync(timeoutSource.Token);
                if (reply.RemoteEndPoint.Address.Equals(remoteEndpoint.Address) && reply.RemoteEndPoint.Port == remoteEndpoint.Port)
                {
                    return new UdpTransportResult(reply.Buffer, reply.RemoteEndPoint);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("接收 UDP 回复超时");
        }
    }

    private static UdpClient CreateClient(UdpEndpointOptions endpoint) =>
        new(new IPEndPoint(IPAddress.Parse(endpoint.LocalAddress), endpoint.LocalPort));
}
