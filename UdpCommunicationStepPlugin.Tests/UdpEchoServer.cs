using System.Net;
using System.Net.Sockets;

namespace UdpCommunicationStepPlugin.Tests;

internal sealed class UdpEchoServer : IAsyncDisposable
{
    private readonly UdpClient _client;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _loop;

    private UdpEchoServer(UdpClient client)
    {
        _client = client;
        Endpoint = (IPEndPoint)client.Client.LocalEndPoint!;
        _loop = EchoAsync(_cancellation.Token);
    }

    public IPEndPoint Endpoint { get; }

    public static Task<UdpEchoServer> StartAsync()
        => Task.FromResult(new UdpEchoServer(new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))));

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        _client.Dispose();
        try { await _loop; } catch (OperationCanceledException) { }
        _cancellation.Dispose();
    }

    private async Task EchoAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var request = await _client.ReceiveAsync(cancellationToken);
            await _client.SendAsync(request.Buffer, request.RemoteEndPoint, cancellationToken);
        }
    }
}
