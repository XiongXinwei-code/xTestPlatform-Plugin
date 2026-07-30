using System.Net;
using System.Net.Sockets;

namespace UdpCommunicationStepPlugin.Infrastructure;

public static class UdpTransport
{
    public static async Task SendAsync(IPEndPoint remoteEndpoint, int localPort, byte[] payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remoteEndpoint);
        ArgumentNullException.ThrowIfNull(payload);

        using var client = CreateClient(remoteEndpoint.AddressFamily, localPort);
        await client.SendAsync(payload, remoteEndpoint, cancellationToken);
    }

    public static async Task<byte[]> SendAndReceiveAsync(
        IPEndPoint remoteEndpoint,
        int localPort,
        byte[] payload,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remoteEndpoint);
        ArgumentNullException.ThrowIfNull(payload);

        using var client = CreateClient(remoteEndpoint.AddressFamily, localPort);
        await client.SendAsync(payload, remoteEndpoint, cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var response = await client.ReceiveAsync(timeoutSource.Token);
        return response.Buffer;
    }

    private static UdpClient CreateClient(AddressFamily addressFamily, int localPort)
    {
        var client = new UdpClient(addressFamily);
        var address = addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        client.Client.Bind(new IPEndPoint(address, localPort));
        return client;
    }
}
