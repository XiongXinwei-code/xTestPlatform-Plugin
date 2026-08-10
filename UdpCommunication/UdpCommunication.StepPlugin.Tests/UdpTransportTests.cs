using System.Net;
using System.Net.Sockets;
using System.Text;
using UdpCommunication.Transport;
using Xunit;

namespace UdpCommunication.Tests;

public sealed class UdpTransportTests
{
    [Fact]
    public async Task SendAsync_AlreadyCancelled_ThrowsBeforeOpeningSocket()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        {
            using var transport = new UdpTransport("127.0.0.1", 0);
            return transport.SendAsync(
                "PING"u8.ToArray(),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9000),
                cancelled.Token);
        });
    }

    [Fact]
    public async Task SendAndReceiveAsync_NonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        using var transport = new UdpTransport("127.0.0.1", 0);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            transport.SendAndReceiveAsync(
                "PING"u8.ToArray(),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9000),
                TimeSpan.Zero,
                CancellationToken.None));
    }

    [Fact]
    public async Task SendAndReceiveAsync_AlreadyCancelled_ThrowsBeforeSending()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        {
            using var transport = new UdpTransport("127.0.0.1", 0);
            return transport.SendAndReceiveAsync(
                "PING"u8.ToArray(),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9000),
                TimeSpan.FromSeconds(1),
                cancelled.Token);
        });
    }

    [Fact]
    public async Task SendAndReceiveAsync_BindsConfiguredPortAndReceivesReply()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;

        var responder = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(Encoding.UTF8.GetBytes("ACK"), request.RemoteEndPoint);
        });

        using var transport = new UdpTransport("127.0.0.1", 24567);
        var result = await transport.SendAndReceiveAsync(
            Encoding.UTF8.GetBytes("PING"),
            new IPEndPoint(IPAddress.Parse("127.0.0.1"), serverPort),
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        await responder;
        Assert.Equal("ACK", Encoding.UTF8.GetString(result.Payload));
    }
}
