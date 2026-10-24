using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Ethernet.DoIP;
using Ethernet.SomeIP;
using Xunit;

namespace PluginRegression.Tests;

public sealed class NetworkRegressionTests
{
    [Theory]
    [InlineData(uint.MaxValue)]
    [InlineData(0x80000000u)]
    public void Someip_invalid_length_returns_null_without_throwing(uint length)
    {
        byte[] data = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), length);
        Assert.Null(SomeIpMessage.TryDecode(data));
    }

    [Fact]
    public async Task Doip_rejects_response_from_another_logical_address()
    {
        using var pair = await SocketPair.CreateAsync();
        using var client = new DoipClient(pair.Client, 0x0E00, 1000);
        using var deadline = new CancellationTokenSource(3000);
        var peer = Task.Run(async () =>
        {
            await ReadFrame(pair.Peer, deadline.Token);
            await SendFrame(pair.Peer, 0x8001, [0x99, 0x99, 0x88, 0x88, 0x50, 0x01], deadline.Token);
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => client.DiagRequestAsync(0x1000, [0x10, 0x01], deadline.Token));
        await peer;
    }

    [Fact]
    public async Task Doip_ack_stream_does_not_restart_the_whole_request_deadline()
    {
        using var pair = await SocketPair.CreateAsync();
        using var client = new DoipClient(pair.Client, 0x0E00, 80);
        using var deadline = new CancellationTokenSource(900);
        var peer = Task.Run(async () =>
        {
            try
            {
                await ReadFrame(pair.Peer, deadline.Token);
                while (!deadline.IsCancellationRequested)
                {
                    await SendFrame(pair.Peer, 0x8002, [0x10, 0, 0x0E, 0, 0], deadline.Token);
                    await Task.Delay(10, deadline.Token);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException) { }
        });
        var clock = Stopwatch.StartNew();
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => client.DiagRequestAsync(0x1000, [0x10, 0x01], deadline.Token));
            Assert.True(clock.ElapsedMilliseconds < 650, $"elapsed={clock.ElapsedMilliseconds}");
        }
        finally { deadline.Cancel(); await peer; }
    }

    private static async Task<byte[]> ReadFrame(TcpClient client, CancellationToken ct)
    {
        var header = new byte[8];
        await client.GetStream().ReadExactlyAsync(header, ct);
        var payload = new byte[BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4))];
        await client.GetStream().ReadExactlyAsync(payload, ct);
        return payload;
    }

    [Fact]
    public async Task Closing_a_pending_connection_prevents_its_late_registration()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(3000);
        string name = "audit-" + Guid.NewGuid().ToString("N");
        var opening = DoipConnectionManager.ConnectAsync(name, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            0x0E00, 0, 2000, deadline.Token);
        using var peer = await listener.AcceptTcpClientAsync(deadline.Token);
        await ReadFrame(peer, deadline.Token);
        var closing = DoipConnectionManager.CloseAsync(name, deadline.Token);
        try { await SendFrame(peer, 0x0006, [0x0E, 0, 0x10, 0, 0x10, 0, 0, 0, 0], deadline.Token); }
        catch (Exception ex) when (ex is IOException or SocketException) { }
        var error = await Record.ExceptionAsync(async () => { using var result = await opening; });
        await closing;
        Assert.NotNull(error);
        Assert.Throws<InvalidOperationException>(() => DoipConnectionManager.Get(name));
    }

    private static async Task SendFrame(TcpClient client, ushort type, byte[] payload, CancellationToken ct)
    {
        var bytes = new byte[8 + payload.Length];
        bytes[0] = 2; bytes[1] = 0xFD;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), type);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), (uint)payload.Length);
        payload.CopyTo(bytes, 8);
        await client.GetStream().WriteAsync(bytes, ct);
    }

    private sealed class SocketPair(TcpClient client, TcpClient peer) : IDisposable
    {
        public TcpClient Client { get; } = client;
        public TcpClient Peer { get; } = peer;
        public static async Task<SocketPair> CreateAsync()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var accepting = listener.AcceptTcpClientAsync();
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            return new SocketPair(client, await accepting);
        }
        public void Dispose() { Client.Dispose(); Peer.Dispose(); }
    }
}
