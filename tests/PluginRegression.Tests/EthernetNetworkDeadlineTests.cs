using System.Net;
using System.Net.Sockets;
using Ethernet;
using Ethernet.Executors;
using Ethernet.Models;
using Ethernet.Runtime;
using Ethernet.SomeIP;
using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetNetworkDeadlineTests
{
    [Fact]
    public async Task Someip_udp_dns_is_async_cancellable_and_inside_the_request_budget()
    {
        var network = new ControlledNetwork { BlockResolve = true };
        var plugin = new SomeIpRequestPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpRequestSetting
        { RemoteHost = "\"controlled.invalid\"", TimeoutMs = 40 });
        var result = await new SomeIpRequestExecutor(network).ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("40", result.StepResult.Error!.Message);
        Assert.True(network.ResolveExited);
        Assert.Equal(0, network.UdpSends);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fire_and_forget_has_a_real_deadline_for_dns_or_tcp_connect(bool tcp)
    {
        var network = new ControlledNetwork { BlockResolve = !tcp, BlockConnect = tcp };
        var plugin = new SomeIpFireAndForgetPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpFireAndForgetSetting
        { RemoteHost = "\"controlled.invalid\"", Transport = tcp ? SomeIpTransport.Tcp : SomeIpTransport.Udp, SendTimeoutMs = 40 });
        var result = await new SomeIpFireAndForgetExecutor(network).ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("40", result.StepResult.Error!.Message);
        Assert.Equal(0, network.UdpSends + network.TcpWrites);
        Assert.True(tcp ? network.ConnectExited : network.ResolveExited);
    }

    [Fact]
    public async Task Udp_send_waits_for_the_cancelled_socket_operation_to_exit_and_reports_unknown_submission()
    {
        var network = new ControlledNetwork { BlockUdpSend = true };
        var plugin = new UdpSendPlugin();
        var (context, _) = TestContext.Create(plugin, new UdpSendSetting
        { RemoteHost = "\"127.0.0.1\"", SendTimeoutMs = 40 });
        var result = await new UdpSendExecutor(network).ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("40", result.StepResult.Error!.Message);
        Assert.Contains("未知", result.StepResult.Error.Message);
        Assert.True(network.SendExited);
        Assert.Equal(1, network.UdpSends);
    }

    [Fact]
    public async Task Someip_udp_resolution_and_send_share_one_deadline_and_do_not_retry()
    {
        var network = new ControlledNetwork { ResolveDelayMs = 15, BlockUdpSend = true };
        var plugin = new SomeIpRequestPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpRequestSetting
        { RemoteHost = "\"controlled.invalid\"", TimeoutMs = 80 });
        var result = await new SomeIpRequestExecutor(network).ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Equal(network.ResolveToken, network.SendToken);
        Assert.Equal(1, network.UdpSends);
        Assert.True(network.SendExited);
    }

    [Fact]
    public async Task Cancelling_dns_uses_the_original_caller_semantics_and_never_submits_a_frame()
    {
        var network = new ControlledNetwork { BlockResolve = true };
        var plugin = new SomeIpFireAndForgetPlugin();
        var (context, _) = TestContext.Create(plugin, new SomeIpFireAndForgetSetting
        { RemoteHost = "\"controlled.invalid\"", SendTimeoutMs = 2000 });
        using var caller = new CancellationTokenSource(4000);
        var operation = new SomeIpFireAndForgetExecutor(network).ExecuteAsync(context, caller.Token);
        await network.ResolveEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        caller.Cancel();
        Assert.Equal(TestStatus.Aborted, (await operation).StepResult.Status);
        Assert.True(network.ResolveExited);
        Assert.Equal(0, network.UdpSends);
    }

    [Fact]
    public async Task Precancelled_operations_do_not_resolve_connect_or_send()
    {
        var network = new ControlledNetwork();
        IStepPlugin[] plugins = [new UdpSendPlugin(), new SomeIpFireAndForgetPlugin(), new SomeIpRequestPlugin()];
        IStepExecutor[] executors = [new UdpSendExecutor(network), new SomeIpFireAndForgetExecutor(network), new SomeIpRequestExecutor(network)];
        using var caller = new CancellationTokenSource(); caller.Cancel();
        for (int i = 0; i < plugins.Length; i++)
        {
            var (context, _) = TestContext.Create(plugins[i], plugins[i].CreateSerializer().CreateDefault());
            Assert.Equal(TestStatus.Aborted, (await executors[i].ExecuteAsync(context, caller.Token)).StepResult.Status);
        }
        Assert.Equal(0, network.Resolves + network.Connects + network.UdpSends + network.TcpWrites);
    }

    private sealed class ControlledNetwork : IEthernetNetworkIo
    {
        public bool BlockResolve, BlockConnect, BlockUdpSend;
        public int ResolveDelayMs, Resolves, Connects, UdpSends, TcpWrites;
        public bool ResolveExited, ConnectExited, SendExited;
        public CancellationToken ResolveToken, SendToken;
        public TaskCompletionSource ResolveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
        {
            Resolves++; ResolveToken = ct; ResolveEntered.TrySetResult();
            try
            {
                if (ResolveDelayMs > 0) await Task.Delay(ResolveDelayMs, ct);
                if (BlockResolve) await Task.Delay(Timeout.Infinite, ct);
                return [IPAddress.Loopback];
            }
            finally { ResolveExited = true; }
        }

        public async Task ConnectTcpAsync(TcpClient client, string host, int port, CancellationToken ct)
        {
            Connects++;
            try { if (BlockConnect) await Task.Delay(Timeout.Infinite, ct); }
            finally { ConnectExited = true; }
        }

        public Task WriteTcpAsync(NetworkStream stream, byte[] data, CancellationToken ct)
        { TcpWrites++; ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }

        public async Task SendUdpAsync(UdpClient client, IPEndPoint endpoint, byte[] data, CancellationToken ct)
        {
            UdpSends++; SendToken = ct;
            try { if (BlockUdpSend) await Task.Delay(Timeout.Infinite, ct); }
            finally { SendExited = true; }
        }
    }
}
