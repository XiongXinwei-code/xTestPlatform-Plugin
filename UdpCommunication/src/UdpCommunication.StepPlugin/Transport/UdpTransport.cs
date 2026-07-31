using System.Net;
using System.Net.Sockets;

namespace UdpCommunication.StepPlugin.Transport;

public sealed class UdpTransport
{
    public async Task SendAsync(UdpEndpointOptions endpoint, ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint);
        await client.SendAsync(request, new IPEndPoint(IPAddress.Parse(endpoint.RemoteAddress), endpoint.RemotePort), cancellationToken);
    }

    public async Task<UdpTransportResult> SendAndReceiveAsync(UdpEndpointOptions endpoint, ReadOnlyMemory<byte> request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint);
        await client.SendAsync(request, new IPEndPoint(IPAddress.Parse(endpoint.RemoteAddress), endpoint.RemotePort), cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var reply = await client.ReceiveAsync(timeoutSource.Token);
            return new UdpTransportResult(reply.Buffer, reply.RemoteEndPoint);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("接收 UDP 回复超时");
        }
    }

    private static UdpClient CreateClient(UdpEndpointOptions endpoint) =>
        new(new IPEndPoint(IPAddress.Parse(endpoint.LocalAddress), endpoint.LocalPort));
}
