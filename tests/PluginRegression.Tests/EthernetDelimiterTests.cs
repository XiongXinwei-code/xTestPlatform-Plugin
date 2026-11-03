using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Ethernet;
using Ethernet.Models;
using Ethernet.Runtime;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetDelimiterTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task Actual_peer_close_still_returns_error_without_output()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var guard = new CancellationTokenSource(10000);
        var plugin = new TcpReceivePlugin();
        var (ctx, state) = TestContext.Create(plugin, new TcpReceiveSetting { UseDelimiter = true, ResultVariable = "Locals.reply" });
        state.DeclaredVariables["Locals.reply"] = "previous";
        await TcpConnectionManager.ConnectAsync("TCP1", "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, 2000, guard.Token, ctx.Resources);
        using var peer = await listener.AcceptTcpClientAsync(guard.Token);
        await peer.GetStream().WriteAsync(Encoding.ASCII.GetBytes("unfinished"), guard.Token);
        peer.Close();
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("对端已关闭", result.StepResult.Error!.Message);
        Assert.False(state.Variables.ContainsKey("Locals.reply"));
        await TcpConnectionManager.CloseAsync("TCP1", guard.Token, ctx.Resources);
        state.SetStep(plugin, new TcpReceiveSetting());
        Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
    }

    [Fact]
    public async Task Native_cancelled_tcp_read_records_real_socket_usability()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var guard = new CancellationTokenSource(10000);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, guard.Token);
        using var peer = await listener.AcceptTcpClientAsync(guard.Token); var stream = client.GetStream();
        var diagnostics = new StringBuilder(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + "\n");
        var bytes = new byte[65535];
        await peer.GetStream().WriteAsync(new byte[] {65}, guard.Token);
        Assert.Equal(1, await stream.ReadAsync(bytes, guard.Token));
        using (var deadline = new CancellationTokenSource(70))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.ReadAsync(bytes, deadline.Token));
        diagnostics.AppendLine($"After cancellation: Connected={client.Connected}, SocketConnected={client.Client.Connected}");
        try { client.GetStream(); diagnostics.AppendLine("GetStream success"); }
        catch (Exception ex) { diagnostics.AppendLine("GetStream: " + ex.Message); }
        await peer.GetStream().WriteAsync(new byte[] {66}, guard.Token);
        bool cachedReadWorks;
        using (var deadline = new CancellationTokenSource(1000))
        {
            try { cachedReadWorks = await stream.ReadAsync(bytes, deadline.Token) == 1 && bytes[0] == 66; diagnostics.AppendLine("Cached read=" + cachedReadWorks); }
            catch (Exception ex) { cachedReadWorks = false; diagnostics.AppendLine("Cached read: " + ex); }
        }
        diagnostics.AppendLine($"After cached read: Connected={client.Connected}");
        output.WriteLine(diagnostics.ToString());
        Assert.True(cachedReadWorks, diagnostics.ToString());
    }

    [Fact]
    public async Task Fragmented_delimiter_preserves_coalesced_second_frame_and_binary_tail()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var guard = new CancellationTokenSource(10000);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, guard.Token);
        using var server = await listener.AcceptTcpClientAsync(guard.Token);
        var buffer = TcpReceiveBuffer.For(client);
        var first = buffer.ReadAsync(client, 0, [13, 10], false, 100, guard.Token, () => { });
        await server.GetStream().WriteAsync(Encoding.ASCII.GetBytes("first\r"), guard.Token);
        await server.GetStream().WriteAsync(Encoding.ASCII.GetBytes("\nsecond\r\nTAIL"), guard.Token);
        Assert.Equal("first", Encoding.ASCII.GetString(await first));
        Assert.Equal("second\r\n", Encoding.ASCII.GetString(await buffer.ReadAsync(client, 0, [13, 10], true, 100, guard.Token, () => { })));
        Assert.Equal("TAIL", Encoding.ASCII.GetString(await buffer.ReadAsync(client, 4, null, false, 100, guard.Token, () => { })));
    }

    [Fact]
    public async Task Delimiter_timeout_reports_error_without_output_and_preserves_partial_for_same_connection()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var guard = new CancellationTokenSource(10000);
        var plugin = new TcpReceivePlugin();
        var (ctx, state) = TestContext.Create(plugin, new TcpReceiveSetting { UseDelimiter = true, TimeoutMs = 100, ResultVariable = "Locals.reply", Encoding = EthernetDataEncoding.Ascii });
        state.DeclaredVariables["Locals.reply"] = "before";
        await TcpConnectionManager.ConnectAsync("TCP1", "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, 2000, guard.Token, ctx.Resources);
        using var server = await listener.AcceptTcpClientAsync(guard.Token);
        await server.GetStream().WriteAsync(Encoding.ASCII.GetBytes("partial"), guard.Token);
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token);
        Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.False(state.Variables.ContainsKey("Locals.reply"));
        await server.GetStream().WriteAsync(new byte[] {13, 10}, guard.Token);
        state.SetStep(plugin, new TcpReceiveSetting { UseDelimiter = true, TimeoutMs = 2000, Encoding = EthernetDataEncoding.Ascii });
        var resumed = await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token);
        Assert.True(resumed.StepResult.Status == TestStatus.Completed, resumed.StepResult.Error?.Message + "\n" + resumed.StepResult.Error?.Detail);
        Assert.Equal("partial", resumed.StepResult.Value);
        var send = new TcpSendPlugin(); state.SetStep(send, new TcpSendSetting { Data = "\"41 42\"" });
        var sent = await send.CreateExecutor().ExecuteAsync(ctx, guard.Token);
        Assert.True(sent.StepResult.Status == TestStatus.Completed, sent.StepResult.Error?.Message);
        var echoed = new byte[2]; await server.GetStream().ReadExactlyAsync(echoed, guard.Token);
        Assert.Equal(new byte[] { 0x41, 0x42 }, echoed);
        await TcpConnectionManager.CloseAsync("TCP1", guard.Token, ctx.Resources);
    }

    [Fact]
    public async Task Maximum_length_is_enforced_without_waiting_for_socket_timeout()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var guard = new CancellationTokenSource(10000);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, guard.Token);
        using var server = await listener.AcceptTcpClientAsync(guard.Token);
        await server.GetStream().WriteAsync(Encoding.ASCII.GetBytes("12345678"), guard.Token);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => TcpReceiveBuffer.For(client).ReadAsync(client, 0, [13, 10], false, 8, guard.Token, () => { }));
        Assert.Contains("MaxBytes=8", error.Message);
    }

    [Fact]
    public async Task Reopened_connection_cannot_inherit_previous_socket_tail()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var guard = new CancellationTokenSource(10000);
        var (ctx, _) = TestContext.Create(new TcpOpenPlugin(), new TcpOpenSetting());
        var old = await TcpConnectionManager.ConnectAsync("TCP1", "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, 2000, guard.Token, ctx.Resources);
        using var first = await listener.AcceptTcpClientAsync(guard.Token);
        await first.GetStream().WriteAsync(Encoding.ASCII.GetBytes("A\r\nOLD\r\n"), guard.Token);
        Assert.Equal("A", Encoding.ASCII.GetString(await TcpReceiveBuffer.For(old).ReadAsync(old, 0, [13, 10], false, 100, guard.Token, () => { })));
        await TcpConnectionManager.CloseAsync("TCP1", guard.Token, ctx.Resources);
        var next = await TcpConnectionManager.ConnectAsync("TCP1", "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, 2000, guard.Token, ctx.Resources);
        using var second = await listener.AcceptTcpClientAsync(guard.Token);
        await second.GetStream().WriteAsync(Encoding.ASCII.GetBytes("NEW\r\n"), guard.Token);
        Assert.Equal("NEW", Encoding.ASCII.GetString(await TcpReceiveBuffer.For(next).ReadAsync(next, 0, [13, 10], false, 100, guard.Token, () => { })));
        await TcpConnectionManager.CloseAsync("TCP1", guard.Token, ctx.Resources);
    }
}
