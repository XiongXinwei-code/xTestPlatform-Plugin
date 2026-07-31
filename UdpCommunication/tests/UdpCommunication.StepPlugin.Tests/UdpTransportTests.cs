using System.Net;
using System.Net.Sockets;
using System.Text;
using UdpCommunication.StepPlugin.Transport;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class UdpTransportTests
{
    [Fact]
    public async Task SendAndReceiveAsync_BindsConfiguredPortAndReceivesReply()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var clientPort = 0;
        var responder = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            clientPort = request.RemoteEndPoint.Port;
            await server.SendAsync(Encoding.UTF8.GetBytes("ACK"), request.RemoteEndPoint);
        });
        var result = await new UdpTransport().SendAndReceiveAsync(
            new UdpEndpointOptions("127.0.0.1", 24567, "127.0.0.1", serverPort),
            Encoding.UTF8.GetBytes("PING"), TimeSpan.FromSeconds(1), CancellationToken.None);
        await responder;
        Assert.Equal("ACK", Encoding.UTF8.GetString(result.Payload));
        Assert.Equal(24567, clientPort);
    }
}
