using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Ethernet;
using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ReauditEthernetDeadlineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tcp_logging_delay_does_not_reclassify_already_completed_io_as_timeout(bool read)
    {
        using var guard = new CancellationTokenSource(15000);
        using var peer = new TcpListener(IPAddress.Loopback, 0);
        peer.Start();
        var context = DispatchProxy.Create<IExecutionContext, ResponseLogGateProxy>();
        var state = (ResponseLogGateProxy)(object)context;
        string name = "deadline-" + Guid.NewGuid().ToString("N");
        IStepPlugin plugin = read ? new TcpReceivePlugin() : new TcpSendPlugin();
        object setting = read
            ? new TcpReceiveSetting { ConnectionName = $"\"{name}\"", TimeoutMs = 250, ExpectedLength = 1, ResultVariable = "Locals.rx" }
            : new TcpSendSetting { ConnectionName = $"\"{name}\"", SendTimeoutMs = 250, Data = "\"A5\"", Encoding = EthernetDataEncoding.Hex };
        state.SetStep(plugin, setting);
        state.BlockPrefix = read ? "TCP 接收:" : "TCP 发送:";
        var connecting = TcpConnectionManager.ConnectAsync(name, "127.0.0.1", ((IPEndPoint)peer.LocalEndpoint).Port, 3000, guard.Token, context.Resources);
        using var connected = await peer.AcceptTcpClientAsync(guard.Token);
        await connecting;
        // Send may complete synchronously and enter the controlled logger before ExecuteAsync returns.
        var running = Task.Run(() => plugin.CreateExecutor().ExecuteAsync(context, guard.Token));
        try
        {
            if (read) await connected.GetStream().WriteAsync(new byte[] { 0xA5 }, guard.Token);
            else await connected.GetStream().ReadExactlyAsync(new byte[1], guard.Token);
            await state.LogEntered.Task.WaitAsync(guard.Token);
            await Task.Delay(350, guard.Token);
            state.ReleaseLog.Set();
            var result = await running.WaitAsync(guard.Token);
            // I/O已完成后才进入日志：原配置约束I/O等待，初次期待Error属于无效探针。
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            if (read) Assert.Equal("A5", state.Variables["Locals.rx"]);
            else Assert.Empty(state.Variables);
        }
        finally
        {
            state.ReleaseLog.Set(); await running.WaitAsync(guard.Token);
            await TcpConnectionManager.CloseAsync(name, guard.Token, context.Resources, 2000);
        }
    }

    [Fact]
    public async Task Udp_logging_delay_keeps_the_existing_successful_receive_contract()
    {
        using var guard = new CancellationTokenSource(10000);
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        reservation.Close();
        using var peer = new UdpClient();
        var context = DispatchProxy.Create<IExecutionContext, ResponseLogGateProxy>();
        var state = (ResponseLogGateProxy)(object)context;
        var plugin = new UdpReceivePlugin();
        state.SetStep(plugin, new UdpReceiveSetting { LocalPort = port, TimeoutMs = 250, ResultVariable = "Locals.rx" });
        state.BlockPrefix = "UDP 接收:";
        var running = plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
        try
        {
            await peer.SendAsync(new byte[] { 0xA5 }, new IPEndPoint(IPAddress.Loopback, port), guard.Token);
            await state.LogEntered.Task.WaitAsync(guard.Token);
            await Task.Delay(350, guard.Token);
            state.ReleaseLog.Set();
            var result = await running.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.Equal("A5", state.Variables["Locals.rx"]);
        }
        finally { state.ReleaseLog.Set(); await running.WaitAsync(guard.Token); }
    }
}
