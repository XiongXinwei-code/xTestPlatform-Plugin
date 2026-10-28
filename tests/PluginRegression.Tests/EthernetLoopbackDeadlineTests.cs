using System.Net;
using System.Net.Sockets;
using Ethernet;
using Ethernet.Models;
using Ethernet.Runtime;
using Ethernet.SomeIP;
using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetLoopbackDeadlineTests
{
    [Fact]
    public async Task Someip_udp_hostname_request_completes_with_the_real_async_dns_and_socket_path()
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var guard = new CancellationTokenSource(15000);
        var plugin = new SomeIpRequestPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpRequestSetting
        { RemoteHost = "\"localhost\"", RemotePort = QuotePort(peer.Client.LocalEndPoint!), TimeoutMs = 2000 });
        var execution = plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
        var incoming = await peer.ReceiveAsync(guard.Token);
        var response = Assert.IsType<SomeIpMessage>(SomeIpMessage.TryDecode(incoming.Buffer));
        response.MessageType = SomeIpMessageType.Response; response.Payload = [0xA5, 0x5A];
        await peer.SendAsync(response.Encode(), incoming.RemoteEndPoint, guard.Token);
        var result = await execution;
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.Equal("A5 5A", result.StepResult.Value);
    }

    [Fact]
    public async Task Someip_tcp_request_preserves_complete_response_framing()
    {
        using var peer = new TcpListener(IPAddress.Loopback, 0); peer.Start();
        using var guard = new CancellationTokenSource(15000);
        var plugin = new SomeIpRequestPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpRequestSetting
        { RemoteHost = "\"127.0.0.1\"", RemotePort = QuotePort(peer.LocalEndpoint), Transport = SomeIpTransport.Tcp, TimeoutMs = 2000 });
        var execution = plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
        var accepting = peer.AcceptTcpClientAsync(guard.Token).AsTask();
        await Task.WhenAny(execution, accepting);
        Assert.True(!execution.IsCompleted || (await execution).StepResult.Status == TestStatus.Completed,
            execution.IsCompleted ? (await execution).StepResult.Error?.Detail : "请求在 TCP 接受前异常结束");
        using var socket = await accepting;
        var response = await SomeIpRequestExecutor.ReceiveTcpMessageAsync(socket.GetStream(), guard.Token);
        response.MessageType = SomeIpMessageType.Response; response.Payload = [0x42];
        var encoded = response.Encode();
        await socket.GetStream().WriteAsync(encoded.AsMemory(0, 7), guard.Token);
        await socket.GetStream().WriteAsync(encoded.AsMemory(7), guard.Token);
        var result = await execution;
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.Equal("42", result.StepResult.Value);
    }

    [Fact]
    public async Task Fire_and_forget_udp_hostname_sends_one_valid_frame_without_a_response()
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var guard = new CancellationTokenSource(15000);
        var plugin = new SomeIpFireAndForgetPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpFireAndForgetSetting
        { RemoteHost = "\"localhost\"", RemotePort = QuotePort(peer.Client.LocalEndPoint!), SendTimeoutMs = 2000 });
        var result = await plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        var message = Assert.IsType<SomeIpMessage>(SomeIpMessage.TryDecode((await peer.ReceiveAsync(guard.Token)).Buffer));
        Assert.Equal(SomeIpMessageType.RequestNoReturn, message.MessageType);
        Assert.Equal(0, peer.Available);
    }

    [Fact]
    public async Task Fire_and_forget_tcp_sends_one_valid_frame_without_a_response()
    {
        using var peer = new TcpListener(IPAddress.Loopback, 0); peer.Start();
        using var guard = new CancellationTokenSource(15000);
        var plugin = new SomeIpFireAndForgetPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpFireAndForgetSetting
        { RemoteHost = "\"127.0.0.1\"", RemotePort = QuotePort(peer.LocalEndpoint), Transport = SomeIpTransport.Tcp, SendTimeoutMs = 2000 });
        var execution = plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
        var accepting = peer.AcceptTcpClientAsync(guard.Token).AsTask();
        await Task.WhenAny(execution, accepting);
        Assert.True(!execution.IsCompleted || (await execution).StepResult.Status == TestStatus.Completed,
            execution.IsCompleted ? (await execution).StepResult.Error?.Detail : "发送在 TCP 接受前异常结束");
        using var socket = await accepting;
        var frame = await SomeIpRequestExecutor.ReceiveTcpMessageAsync(socket.GetStream(), guard.Token);
        Assert.Equal(SomeIpMessageType.RequestNoReturn, frame.MessageType);
        Assert.Equal(TestStatus.Completed, (await execution).StepResult.Status);
    }

    [Fact]
    public async Task Udp_send_preserves_raw_payload_and_local_completion_semantics()
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var guard = new CancellationTokenSource(15000);
        var plugin = new UdpSendPlugin();
        var (context, _) = TestContext.Create(plugin, new UdpSendSetting
        { RemoteHost = "\"127.0.0.1\"", RemotePort = QuotePort(peer.Client.LocalEndPoint!), Data = "\"12 34 AB\"", SendTimeoutMs = 2000 });
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(context, guard.Token)).StepResult.Status);
        Assert.Equal(new byte[] { 0x12, 0x34, 0xAB }, (await peer.ReceiveAsync(guard.Token)).Buffer);
    }

    [Fact]
    public async Task Fire_and_forget_write_deadline_tracks_actual_write_exit_and_reports_unknown_without_retry()
    {
        using var peer = new TcpListener(IPAddress.Loopback, 0); peer.Start();
        using var guard = new CancellationTokenSource(15000);
        var network = new BlockedTcpWrite();
        var plugin = new SomeIpFireAndForgetPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpFireAndForgetSetting
        { RemoteHost = "\"127.0.0.1\"", RemotePort = QuotePort(peer.LocalEndpoint), Transport = SomeIpTransport.Tcp, SendTimeoutMs = 500 });
        var execution = new SomeIpFireAndForgetExecutor(network).ExecuteAsync(context, guard.Token);
        using var socket = await peer.AcceptTcpClientAsync(guard.Token);
        var result = await execution;
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("500", result.StepResult.Error!.Message);
        Assert.Contains("未知", result.StepResult.Error.Message);
        await network.WriteCompletion.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(network.WriteExited);
        Assert.Equal(1, network.Writes);
        Assert.Equal(0, socket.Available);
    }

    private static string QuotePort(EndPoint endpoint) => $"\"{((IPEndPoint)endpoint).Port}\"";

    private sealed class BlockedTcpWrite : IEthernetNetworkIo
    {
        public int Writes;
        public bool WriteExited;
        public TaskCompletionSource WriteCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => EthernetNetworkIo.Instance.ResolveAsync(host, ct);
        public Task ConnectTcpAsync(TcpClient client, string host, int port, CancellationToken ct) => EthernetNetworkIo.Instance.ConnectTcpAsync(client, host, port, ct);
        public async Task WriteTcpAsync(NetworkStream stream, byte[] data, CancellationToken ct)
        {
            Writes++;
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { WriteExited = true; WriteCompletion.TrySetResult(); }
        }
        public Task SendUdpAsync(UdpClient client, IPEndPoint endpoint, byte[] data, CancellationToken ct) => throw new InvalidOperationException("Unexpected UDP");
    }
}
