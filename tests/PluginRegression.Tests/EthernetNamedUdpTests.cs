using Ethernet.Runtime;
using System.Reflection;
using xTestPlatform.Core.Engine;
using System.Net;
using System.Net.Sockets;
using Ethernet;
using Ethernet.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetNamedUdpTests
{
    [Fact]
    public async Task Send_deadline_returns_without_falsely_releasing_ignoring_cancellation_call()
    {
        using var guard = new CancellationTokenSource(10000);
        var (ctx, _) = TestContext.Create(new UdpOpenPlugin(), new UdpOpenSetting());
        var connection = await UdpConnectionManager.OpenAsync("send-budget", new IPEndPoint(IPAddress.Loopback, 0), false, 2000, guard.Token, ctx.Resources, () => { });
        var network = new GatedUdpNetwork(false);
        using var budget = new EthernetOperationBudget(80, "测试 UDP 发送", guard.Token);
        var send = connection.SendAsync(network, "127.0.0.1", 12345, [1], budget);
        try
        {
            await network.Entered.Task.WaitAsync(guard.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Throws<InvalidOperationException>(() => connection.CheckOpen());
            await Assert.ThrowsAsync<TimeoutException>(() => UdpConnectionManager.CloseAsync("send-budget", 80, guard.Token, ctx.Resources, () => { }));
        }
        finally { network.Release.TrySetResult(); }
        await UdpConnectionManager.CloseAsync("send-budget", 2000, guard.Token, ctx.Resources, () => { });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Close_timeout_retains_unfinished_io_or_callback_until_exit_before_reopen(bool blockCallback)
    {
        using var guard = new CancellationTokenSource(10000);
        var (ctx, _) = TestContext.Create(new UdpOpenPlugin(), new UdpOpenSetting());
        var connection = await UdpConnectionManager.OpenAsync("drain", new IPEndPoint(IPAddress.Loopback, 0), false, 2000, guard.Token, ctx.Resources, () => { });
        int port = connection.LocalEndpoint.Port;
        var network = new GatedUdpNetwork(blockCallback);
        using var budget = new EthernetOperationBudget(5000, "测试 UDP 发送", guard.Token);
        var send = connection.SendAsync(network, "127.0.0.1", 12345, [1], budget);
        await network.Entered.Task.WaitAsync(guard.Token);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => UdpConnectionManager.CloseAsync("drain", 80, guard.Token, ctx.Resources, () => { }));
            // Socket 本身已经释放；宿主调用/取消回调未退出时仍不得把原代次当作完成。
            using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, port))) { }
            await Assert.ThrowsAsync<TimeoutException>(() => UdpConnectionManager.OpenAsync("drain", new IPEndPoint(IPAddress.Loopback, port), false, 80, guard.Token, ctx.Resources, () => { }));
        }
        finally { network.Release.SetResult(); network.ReleaseCallback.Set(); }
        await Assert.ThrowsAnyAsync<Exception>(() => send);
        var reopened = await UdpConnectionManager.OpenAsync("drain", new IPEndPoint(IPAddress.Loopback, port), false, 2000, guard.Token, ctx.Resources, () => { });
        Assert.NotSame(connection, reopened);
        await UdpConnectionManager.CloseAsync("drain", 2000, guard.Token, ctx.Resources, () => { });
    }

    [Fact]
    public async Task Completed_faulting_cancellation_callback_is_diagnostic_not_permanent_port_ownership()
    {
        using var guard = new CancellationTokenSource(10000);
        var (ctx, _) = TestContext.Create(new UdpOpenPlugin(), new UdpOpenSetting());
        var connection = await UdpConnectionManager.OpenAsync("callback", new IPEndPoint(IPAddress.Loopback, 0), false, 2000, guard.Token, ctx.Resources, () => { });
        int port = connection.LocalEndpoint.Port;
        var network = new GatedUdpNetwork(true, true);
        using var budget = new EthernetOperationBudget(5000, "测试 UDP 发送", guard.Token);
        var send = connection.SendAsync(network, "127.0.0.1", 12345, [1], budget);
        await network.Entered.Task.WaitAsync(guard.Token);
        network.ReleaseCallback.Set();
        await UdpConnectionManager.CloseAsync("callback", 2000, guard.Token, ctx.Resources, () => { });
        await Assert.ThrowsAnyAsync<Exception>(() => send);
        Assert.Contains("回调已退出但报告错误", connection.CloseDiagnostic);
        await UdpConnectionManager.OpenAsync("callback", new IPEndPoint(IPAddress.Loopback, port), false, 2000, guard.Token, ctx.Resources, () => { });
        await UdpConnectionManager.CloseAsync("callback", 2000, guard.Token, ctx.Resources, () => { });
    }

    private sealed class GatedUdpNetwork(bool blockCallback, bool throwCallback = false) : IEthernetNetworkIo
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ReleaseCallback { get; } = new();
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Task.FromResult(new[] { IPAddress.Loopback });
        public Task ConnectTcpAsync(TcpClient client, string host, int port, CancellationToken ct) => throw new NotSupportedException();
        public Task WriteTcpAsync(NetworkStream stream, byte[] data, CancellationToken ct) => throw new NotSupportedException();
        public async Task SendUdpAsync(UdpClient client, IPEndPoint endpoint, byte[] data, CancellationToken ct)
        {
            if (!blockCallback) { Entered.TrySetResult(); await Release.Task; return; }
            using var callback = ct.Register(() =>
            {
                if (!ReleaseCallback.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("测试取消回调等待超限");
                if (throwCallback) throw new InvalidOperationException("受控取消回调错误");
            });
            Entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Log_callback_replacing_udp_connection_cannot_publish_old_endpoint_or_response(bool opening)
    {
        using var guard = new CancellationTokenSource(10000);
        var ctx = DispatchProxy.Create<IExecutionContext, ResponseLogGateProxy>();
        var state = (ResponseLogGateProxy)(object)ctx;
        state.DeclaredVariables["Locals.output"] = "";
        state.BlockPrefix = opening ? "UDP 已绑定:" : "UDP 接收:";
        Task<ExecutionResult> execution;
        if (opening)
        {
            var open = new UdpOpenPlugin();
            state.SetStep(open, new UdpOpenSetting { LocalAddress = "\"127.0.0.1\"", LocalPort = 0, LocalEndpointVariable = "Locals.output" });
            execution = Task.Run(() => open.CreateExecutor().ExecuteAsync(ctx, guard.Token));
        }
        else
        {
            var old = await UdpConnectionManager.OpenAsync("UDP1", new IPEndPoint(IPAddress.Loopback, 0), false, 2000, guard.Token, ctx.Resources, () => { });
            using var sender = new UdpClient();
            await sender.SendAsync(new byte[] { 0xA5 }, old.LocalEndpoint, guard.Token);
            var receive = new UdpReceivePlugin();
            state.SetStep(receive, new UdpReceiveSetting { UseConnection = true, ResultVariable = "Locals.output", TimeoutMs = 3000 });
            execution = Task.Run(() => receive.CreateExecutor().ExecuteAsync(ctx, guard.Token));
        }
        try
        {
            await state.LogEntered.Task.WaitAsync(guard.Token);
            await UdpConnectionManager.CloseAsync("UDP1", 2000, guard.Token, ctx.Resources, () => { });
            var next = await UdpConnectionManager.OpenAsync("UDP1", new IPEndPoint(IPAddress.Loopback, 0), false, 2000, guard.Token, ctx.Resources, () => { });
            state.ReleaseLog.Set();
            Assert.Equal(TestStatus.Error, (await execution.WaitAsync(guard.Token)).StepResult.Status);
            Assert.False(state.Variables.ContainsKey("Locals.output"));
            Assert.Same(next, UdpConnectionManager.Get("UDP1", ctx.Resources));
        }
        finally
        {
            state.ReleaseLog.Set();
            await execution.WaitAsync(guard.Token);
            await UdpConnectionManager.CloseAsync("UDP1", 2000, guard.Token, ctx.Resources, () => { });
        }
    }

    [Fact]
    public async Task Named_udp_keeps_source_port_and_receives_reply_buffered_between_steps()
    {
        using var guard = new CancellationTokenSource(10000);
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var open = new UdpOpenPlugin();
        var (ctx, state) = TestContext.Create(open, new UdpOpenSetting { LocalAddress = "\"127.0.0.1\"", LocalPort = 0, LocalEndpointVariable = "Locals.bound" });
        state.DeclaredVariables["Locals.bound"] = ""; state.DeclaredVariables["Locals.data"] = ""; state.DeclaredVariables["Locals.source"] = "";
        try
        {
            Assert.Equal(TestStatus.Completed, (await open.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
            var bound = IPEndPoint.Parse((string)state.Variables["Locals.bound"]!);
            var send = new UdpSendPlugin();
            state.SetStep(send, new UdpSendSetting { UseConnection = true, RemoteHost = "\"localhost\"", RemotePort = $"\"{((IPEndPoint)peer.Client.LocalEndPoint!).Port}\"", Data = "\"AB CD\"" });
            Assert.Equal(TestStatus.Completed, (await send.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
            var incoming = await peer.ReceiveAsync(guard.Token);
            Assert.Equal(bound.Port, incoming.RemoteEndPoint.Port); Assert.Equal(new byte[] { 0xAB, 0xCD }, incoming.Buffer);
            await peer.SendAsync(new byte[] { 0x12, 0x34 }, incoming.RemoteEndPoint, guard.Token);
            var receive = new UdpReceivePlugin();
            state.SetStep(receive, new UdpReceiveSetting { UseConnection = true, ExpectedLength = 2, ResultVariable = "Locals.data", RemoteEndpointVariable = "Locals.source" });
            Assert.Equal(TestStatus.Completed, (await receive.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
            Assert.Equal("12 34", state.Variables["Locals.data"]); Assert.Equal(peer.Client.LocalEndPoint!.ToString(), state.Variables["Locals.source"]);
        }
        finally { await UdpConnectionManager.CloseAsync("UDP1", 2000, default, ctx.Resources, () => { }); }
    }

    [Fact]
    public async Task Close_interrupts_old_receive_and_same_port_reopens_without_old_result()
    {
        using var guard = new CancellationTokenSource(10000);
        var plugin = new UdpOpenPlugin();
        var (ctx, state) = TestContext.Create(plugin, new UdpOpenSetting { LocalAddress = "\"127.0.0.1\"", LocalPort = 0 });
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
        var previous = UdpConnectionManager.Get("UDP1", ctx.Resources); var port = previous.LocalEndpoint.Port;
        state.DeclaredVariables["Locals.old"] = "";
        var receive = new UdpReceivePlugin(); state.SetStep(receive, new UdpReceiveSetting { UseConnection = true, TimeoutMs = 5000, ResultVariable = "Locals.old" });
        var oldRead = receive.CreateExecutor().ExecuteAsync(ctx, guard.Token);
        var close = new UdpClosePlugin(); state.SetStep(close, new UdpCloseSetting());
        Assert.Equal(TestStatus.Completed, (await close.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
        state.SetStep(plugin, new UdpOpenSetting { LocalAddress = "\"127.0.0.1\"", LocalPort = port });
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
        Assert.NotSame(previous, UdpConnectionManager.Get("UDP1", ctx.Resources));
        var oldResult = await oldRead.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(TestStatus.Error, oldResult.StepResult.Status);
        Assert.Contains("UDP 连接已关闭或替换", oldResult.StepResult.Error!.Message);
        Assert.False(state.Variables.ContainsKey("Locals.old"));
        await UdpConnectionManager.CloseAsync("UDP1", 2000, default, ctx.Resources, () => { });
    }

    [Fact]
    public async Task Receive_timeout_does_not_prevent_close_and_same_port_reopen()
    {
        var (ctx, state) = TestContext.Create(new UdpOpenPlugin(), new UdpOpenSetting());
        var resource = await UdpConnectionManager.OpenAsync("UDP1", new IPEndPoint(IPAddress.Loopback, 0), false, 2000, default, ctx.Resources, () => { });
        int port = resource.LocalEndpoint.Port;
        var read = new UdpReceivePlugin(); state.SetStep(read, new UdpReceiveSetting { UseConnection = true, TimeoutMs = 70 });
        var failed = await read.CreateExecutor().ExecuteAsync(ctx);
        Assert.Equal(TestStatus.Error, failed.StepResult.Status); Assert.Contains("70", failed.StepResult.Error!.Message);
        var close = new UdpClosePlugin(); state.SetStep(close, new UdpCloseSetting());
        Assert.Equal(TestStatus.Completed, (await close.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
        var next = await UdpConnectionManager.OpenAsync("UDP1", new IPEndPoint(IPAddress.Loopback, port), false, 2000, default, ctx.Resources, () => { });
        Assert.Equal(port, next.LocalEndpoint.Port);
        await UdpConnectionManager.CloseAsync("UDP1", 2000, default, ctx.Resources, () => { });
    }

    [Fact]
    public async Task Missing_named_connection_does_not_fall_back_to_temporary_send()
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var plugin = new UdpSendPlugin();
        var (ctx, _) = TestContext.Create(plugin, new UdpSendSetting { UseConnection = true, RemoteHost = "\"127.0.0.1\"", RemotePort = $"\"{((IPEndPoint)peer.Client.LocalEndPoint!).Port}\"" });
        Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
        Assert.Equal(0, peer.Available);
    }

    [Fact]
    public async Task Udp_close_is_idempotent_and_does_not_close_same_name_tcp()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var guard = new CancellationTokenSource(10000);
        var plugin = new UdpClosePlugin(); var (ctx, _) = TestContext.Create(plugin, new UdpCloseSetting { ConnectionName = "\"same\"" });
        var tcp = await TcpConnectionManager.ConnectAsync("same", "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, 2000, guard.Token, ctx.Resources);
        using var server = await listener.AcceptTcpClientAsync(guard.Token);
        await UdpConnectionManager.OpenAsync("same", new IPEndPoint(IPAddress.Loopback, 0), false, 2000, guard.Token, ctx.Resources, () => { });
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
        Assert.Same(tcp, TcpConnectionManager.Get("same", ctx.Resources));
        await TcpConnectionManager.CloseAsync("same", guard.Token, ctx.Resources);
    }
}
