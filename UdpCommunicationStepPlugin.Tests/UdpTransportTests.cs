using System.Net;
using System.Net.Sockets;
using System.Text;
using UdpCommunicationStepPlugin.Infrastructure;
using Xunit;

namespace UdpCommunicationStepPlugin.Tests;

public sealed class UdpTransportTests
{
    [Fact]
    public async Task SendAndReceiveAsync_returns_echoed_datagram()
    {
        await using var server = await UdpEchoServer.StartAsync();

        var reply = await UdpTransport.SendAndReceiveAsync(
            server.Endpoint,
            localPort: 0,
            Encoding.UTF8.GetBytes("PING"),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal("PING", Encoding.UTF8.GetString(reply));
    }

    [Fact]
    public async Task SendAsync_delivers_datagram_to_receiver()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        await UdpTransport.SendAsync(endpoint, localPort: 0, Encoding.UTF8.GetBytes("SEND"), CancellationToken.None);
        var received = await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("SEND", Encoding.UTF8.GetString(received.Buffer));
    }
}
