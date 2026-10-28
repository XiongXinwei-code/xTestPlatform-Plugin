using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using Ethernet;
using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetAbortBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tcp_completed_receive_cannot_publish_after_cancellation_or_connection_close(bool close)
    {
        using var peer = new TcpListener(IPAddress.Loopback, 0); peer.Start();
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        var ctx = DispatchProxy.Create<IExecutionContext, ResponseLogGateProxy>();
        var state = (ResponseLogGateProxy)(object)ctx;
        var plugin = new TcpReceivePlugin();
        string name = "abort-" + Guid.NewGuid().ToString("N");
        state.SetStep(plugin, new TcpReceiveSetting { ConnectionName = $"\"{name}\"", TimeoutMs = 1000, ExpectedLength = 1, ResultVariable = "Locals.rx" });
        state.DeclaredVariables["Locals.rx"] = "";
        state.BlockPrefix = "TCP 接收:";
        var opening = TcpConnectionManager.ConnectAsync(name, "127.0.0.1", ((IPEndPoint)peer.LocalEndpoint).Port, 1000, guard.Token, ctx.Resources);
        using var connected = await peer.AcceptTcpClientAsync(guard.Token);
        await opening;
        var execution = plugin.CreateExecutor().ExecuteAsync(ctx, caller.Token);
        try
        {
            await connected.GetStream().WriteAsync(new byte[] { 0xA5 }, guard.Token);
            await state.LogEntered.Task.WaitAsync(guard.Token);
            if (close) await TcpConnectionManager.CloseAsync(name, guard.Token, ctx.Resources, 1000);
            else caller.Cancel();
            state.ReleaseLog.Set();
            var result = await execution.WaitAsync(guard.Token);
            Assert.Equal(close ? TestStatus.Error : TestStatus.Aborted, result.StepResult.Status);
            Assert.Empty(state.Variables);
        }
        finally { state.ReleaseLog.Set(); caller.Cancel(); await execution.WaitAsync(guard.Token); await TcpConnectionManager.CloseAsync(name, guard.Token, ctx.Resources, 1000); }
    }

    [Fact]
    public async Task Udp_completed_receive_cannot_publish_after_cancellation_in_log_callback()
    {
        using var reserved = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)reserved.Client.LocalEndPoint!).Port; reserved.Close();
        using var peer = new UdpClient();
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        var ctx = DispatchProxy.Create<IExecutionContext, ResponseLogGateProxy>();
        var state = (ResponseLogGateProxy)(object)ctx;
        var plugin = new UdpReceivePlugin();
        state.SetStep(plugin, new UdpReceiveSetting { LocalPort = port, TimeoutMs = 2000, ResultVariable = "Locals.rx" });
        state.DeclaredVariables["Locals.rx"] = "";
        state.BlockPrefix = "UDP 接收:";
        var execution = plugin.CreateExecutor().ExecuteAsync(ctx, caller.Token);
        try
        {
            // The native bind is now owned by a background lifecycle worker. This fixture
            // exercises publication after a receive, so send its one packet only after bind.
            while (!IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(endpoint => endpoint.Port == port))
            {
                Assert.False(execution.IsCompleted, "UDP listener exited before binding its localhost port");
                await Task.Delay(10, guard.Token);
            }
            await peer.SendAsync(new byte[] { 0xA5 }, new IPEndPoint(IPAddress.Loopback, port), guard.Token);
            await state.LogEntered.Task.WaitAsync(guard.Token);
            caller.Cancel(); state.ReleaseLog.Set();
            Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Empty(state.Variables);
        }
        finally { state.ReleaseLog.Set(); caller.Cancel(); await execution.WaitAsync(guard.Token); }
    }
}
