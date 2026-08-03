using System.Net;
using System.Net.Sockets;
using System.Text;
using UdpCommunication.StepPlugin.Transport;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class UdpTransportTests
{
    [Fact]
    public async Task SendAsync_AlreadyCancelled_ThrowsBeforeOpeningSocket()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UdpTransport().SendAsync(
            new UdpEndpointOptions("not-an-ip", 0, "127.0.0.1", 9000),
            "PING"u8.ToArray(), cancelled.Token));
    }

    [Fact]
    public async Task SendAndReceiveAsync_NonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new UdpTransport().SendAndReceiveAsync(
            new UdpEndpointOptions("127.0.0.1", 0, "127.0.0.1", 9000),
            "PING"u8.ToArray(), TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public async Task SendAndReceiveAsync_AlreadyCancelled_ThrowsBeforeParsingEndpointOrOpeningSocket()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UdpTransport().SendAndReceiveAsync(
            new UdpEndpointOptions("not-an-ip", 0, "also-not-an-ip", 9000),
            "PING"u8.ToArray(), TimeSpan.FromSeconds(1), cancelled.Token));
    }

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

    [Fact]
    public async Task SendAndReceiveAsync_IgnoresReplyFromEndpointOtherThanConfiguredRemote()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var unexpectedSender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var responder = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await unexpectedSender.SendAsync(Encoding.UTF8.GetBytes("SPOOFED"), request.RemoteEndPoint);
            await Task.Delay(50);
            await server.SendAsync(Encoding.UTF8.GetBytes("ACK"), request.RemoteEndPoint);
        });

        var result = await new UdpTransport().SendAndReceiveAsync(
            new UdpEndpointOptions("127.0.0.1", 0, "127.0.0.1", serverPort),
            Encoding.UTF8.GetBytes("PING"), TimeSpan.FromSeconds(1), CancellationToken.None);

        await responder;
        Assert.Equal("ACK", Encoding.UTF8.GetString(result.Payload));
        Assert.Equal(serverPort, result.RemoteEndPoint.Port);
    }
}
