using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Ethernet.DoIP;
using Ethernet.DoIP.Models;
using Ethernet.SomeIP;
using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ReauditEthernetProtocolTests
{
    [Theory]
    [InlineData(SomeIpTransport.Udp, SomeIpMessageType.Response)]
    [InlineData(SomeIpTransport.Udp, SomeIpMessageType.Error)]
    [InlineData(SomeIpTransport.Tcp, SomeIpMessageType.Response)]
    [InlineData(SomeIpTransport.Tcp, SomeIpMessageType.Error)]
    public async Task Someip_response_requires_the_complete_client_and_session_request_id(
        SomeIpTransport transport, SomeIpMessageType unrelatedType)
    {
        using var guard = new CancellationTokenSource(15000);
        using var udpPeer = transport == SomeIpTransport.Udp ? new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)) : null;
        using var tcpPeer = transport == SomeIpTransport.Tcp ? new TcpListener(IPAddress.Loopback, 0) : null;
        tcpPeer?.Start();
        int port = ((IPEndPoint)(udpPeer?.Client.LocalEndPoint ?? tcpPeer!.LocalEndpoint)!).Port;
        var plugin = new SomeIpRequestPlugin();
        var (context, state) = TestContext.Create(plugin, new SomeIpRequestSetting
        {
            RemoteHost = "\"127.0.0.1\"", RemotePort = $"\"{port}\"", Transport = transport,
            ClientId = "\"0x2211\"", TimeoutMs = 2000, ResultVariable = "Locals.reply"
        });
        state.DeclaredVariables["Locals.reply"] = "";
        var executing = plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
        try
        {
            if (udpPeer != null)
            {
                var received = await udpPeer.ReceiveAsync(guard.Token);
                var request = Assert.IsType<SomeIpMessage>(SomeIpMessage.TryDecode(received.Buffer));
                var unrelated = Reply(request, unrelatedType, sameClient: false);
                var expected = Reply(request, SomeIpMessageType.Response, sameClient: true);
                await udpPeer.SendAsync(unrelated.Encode(), received.RemoteEndPoint, guard.Token);
                await udpPeer.SendAsync(expected.Encode(), received.RemoteEndPoint, guard.Token);
            }
            else
            {
                using var accepted = await tcpPeer!.AcceptTcpClientAsync(guard.Token);
                var stream = accepted.GetStream();
                var request = await SomeIpRequestExecutor.ReceiveTcpMessageAsync(stream, guard.Token);
                byte[] both = [.. Reply(request, unrelatedType, sameClient: false).Encode(),
                    .. Reply(request, SomeIpMessageType.Response, sameClient: true).Encode()];
                // 单次写入保证 TCP 字节流里错误 ClientId 的完整报文先于正确报文。
                await stream.WriteAsync(both, guard.Token);
                var result = await executing.WaitAsync(guard.Token);
                Assert.Equal(TestStatus.Completed, result.StepResult.Status);
                Assert.Equal("BE EF", result.StepResult.Value);
            }
            var final = await executing.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Completed, final.StepResult.Status);
            Assert.Equal("BE EF", final.StepResult.Value);
            Assert.Equal("BE EF", state.Variables["Locals.reply"]);
        }
        finally { guard.Cancel(); await executing; }
    }

    [Theory]
    [InlineData("entries", 0x80000000u)]
    [InlineData("entries", 0x7FFFFFFFu)]
    [InlineData("entries", 0xFFFFFFF0u)]
    [InlineData("entries", 0xFFFFFFFFu)]
    [InlineData("entries", 256u)]
    [InlineData("entries", 17u)]
    [InlineData("options", 0x80000000u)]
    [InlineData("options", 0x7FFFFFFFu)]
    [InlineData("options", 0xFFFFFFF0u)]
    [InlineData("options", 0xFFFFFFFFu)]
    [InlineData("options", 256u)]
    public void Sd_untrusted_uint_region_lengths_are_rejected_without_losing_the_next_valid_offer(string region, uint length)
    {
        var payload = SdPayload();
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(region == "entries" ? 4 : 24), length);
        Assert.Empty(ParseOffers(payload));
        Assert.Equal("Service=0x1234 Instance=0x5678 Ver=1 @ 172.16.0.2:30491/UDP", Assert.Single(ParseOffers(SdPayload())));
    }

    [Theory]
    [InlineData("missingOptionsLength")]
    [InlineData("truncatedOptionHeader")]
    [InlineData("truncatedOptionBody")]
    [InlineData("overdeclaredOptionBody")]
    public void Sd_does_not_publish_an_offer_from_a_truncated_options_region(string invalid)
    {
        var payload = SdPayload();
        if (invalid == "missingOptionsLength") payload = payload[..24];
        else if (invalid == "truncatedOptionHeader") BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(24), 2);
        else if (invalid == "truncatedOptionBody") BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(24), 11);
        else BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(28), 20);
        Assert.Empty(ParseOffers(payload));
        Assert.Single(ParseOffers(SdPayload()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sd_complete_offer_preserves_endpoint_or_source_fallback(bool hasOptions)
    {
        var payload = SdPayload();
        if (!hasOptions)
        {
            payload = payload[..28]; BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(24), 0);
        }
        string expected = hasOptions ? "172.16.0.2:30491/UDP" : "127.0.0.1(SD来源)";
        Assert.Equal($"Service=0x1234 Instance=0x5678 Ver=1 @ {expected}", Assert.Single(ParseOffers(payload)));
    }

    [Theory]
    [InlineData("inverse")]
    [InlineData("shortDeclaration")]
    [InlineData("longDeclaration")]
    [InlineData("zeroDeclaration")]
    [InlineData("highBitDeclaration")]
    [InlineData("trailingBytes")]
    public async Task Doip_malformed_announcement_never_publishes_or_logs_a_vehicle(string invalid)
    {
        var frame = Announcement(0x02, 32);
        if (invalid == "inverse") frame[1] = frame[0];
        else if (invalid == "trailingBytes") Array.Resize(ref frame, frame.Length + 1);
        else BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), invalid switch
        {
            "shortDeclaration" => 31u, "longDeclaration" => 33u, "zeroDeclaration" => 0u,
            "highBitDeclaration" => 0x80000020u, _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        });
        var (result, state) = await DiscoverAsync(frame);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Empty(state.Variables);
        Assert.DoesNotContain(state.Logs, text => text.Contains("VIN=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData((byte)0x02, 32)]
    [InlineData((byte)0x03, 32)]
    [InlineData((byte)0x03, 33)]
    public async Task Doip_complete_announcement_accepts_matching_inverse_without_a_new_version_whitelist(byte version, int payloadSize)
    {
        var (result, state) = await DiscoverAsync(Announcement(version, payloadSize));
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.Equal("VIN=FAKEVIN0000000000, LogicalAddress=0x0E80, IP=127.0.0.1", result.StepResult.Value);
        Assert.Equal(result.StepResult.Value, state.Variables["Locals.vehicle"]);
    }

    private static SomeIpMessage Reply(SomeIpMessage request, SomeIpMessageType type, bool sameClient) => new()
    {
        ServiceId = request.ServiceId, MethodId = request.MethodId, SessionId = request.SessionId,
        ClientId = sameClient ? request.ClientId : (ushort)(request.ClientId ^ 1), MessageType = type,
        // 本次不收紧既有 InterfaceVersion 兼容行为。
        InterfaceVersion = (byte)(request.InterfaceVersion + 1), ReturnCode = type == SomeIpMessageType.Error ? (byte)0x22 : (byte)0,
        Payload = sameClient ? [0xBE, 0xEF] : [0xDE, 0xAD]
    };

    private static byte[] SdPayload()
    {
        var payload = new byte[40]; payload[0] = 0x80;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4), 16);
        payload[8] = 1; payload[11] = 0x10;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(12), 0x1234);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(14), 0x5678);
        payload[16] = 1; payload[19] = 3;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(24), 12);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(28), 9); payload[30] = 0x04;
        payload[32] = 172; payload[33] = 16; payload[34] = 0; payload[35] = 2; payload[37] = 0x11;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(38), 30491);
        return payload;
    }

    private static IReadOnlyList<string> ParseOffers(byte[] payload)
    {
        var frame = new SomeIpMessage { ServiceId = 0xFFFF, MethodId = 0x8100,
            MessageType = SomeIpMessageType.Notification, Payload = payload }.Encode();
        var parser = typeof(SomeIpSdDiscoverExecutor).GetMethod("ParseOffers", BindingFlags.NonPublic | BindingFlags.Static)!;
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(parser.Invoke(null,
            [frame, (ushort)0x1234, new IPEndPoint(IPAddress.Loopback, 30490)]));
    }

    private static byte[] Announcement(byte version, int payloadSize)
    {
        var frame = new byte[8 + payloadSize]; frame[0] = version; frame[1] = unchecked((byte)~version);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0x0004);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), (uint)payloadSize);
        Encoding.ASCII.GetBytes("FAKEVIN0000000000").CopyTo(frame, 8);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(25), 0x0E80);
        return frame;
    }

    private static async Task<(ExecutionResult Result, TestContext State)> DiscoverAsync(byte[] frame)
    {
        using var guard = new CancellationTokenSource(15000);
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var plugin = new DoipVehicleDiscoveryPlugin();
        var (context, state) = TestContext.Create(plugin, new DoipVehicleDiscoverySetting
        {
            BroadcastAddress = "\"127.0.0.1\"", Port = ((IPEndPoint)peer.Client.LocalEndPoint!).Port,
            TimeoutMs = 2000, ResultVariable = "Locals.vehicle"
        });
        state.DeclaredVariables["Locals.vehicle"] = "";
        var executing = plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
        try
        {
            var request = await peer.ReceiveAsync(guard.Token);
            await peer.SendAsync(frame, request.RemoteEndPoint, guard.Token);
            return (await executing.WaitAsync(guard.Token), state);
        }
        finally { guard.Cancel(); await executing; }
    }
}
