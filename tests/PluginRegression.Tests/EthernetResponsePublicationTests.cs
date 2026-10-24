using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Ethernet.DoIP;
using Ethernet.DoIP.Models;
using Ethernet.SomeIP;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetResponsePublicationTests
{
    [Fact]
    public async Task Someip_cancel_during_response_log_does_not_publish_a_result_or_report_completed()
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        var plugin = new SomeIpRequestPlugin();
        var context = DispatchProxy.Create<IExecutionContext, ResponseLogGateProxy>();
        var state = (ResponseLogGateProxy)(object)context;
        state.BlockPrefix = "SOME/IP 响应:";
        state.SetStep(plugin, new SomeIpRequestSetting { RemoteHost = "\"127.0.0.1\"",
            RemotePort = $"\"{((IPEndPoint)peer.Client.LocalEndPoint!).Port}\"", TimeoutMs = 3000,
            ResultVariable = "Locals.rx" });
        var execution = plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
        try
        {
            var packet = await peer.ReceiveAsync(guard.Token);
            var response = Assert.IsType<SomeIpMessage>(SomeIpMessage.TryDecode(packet.Buffer));
            response.MessageType = SomeIpMessageType.Response; response.Payload = [0xA5];
            await peer.SendAsync(response.Encode(), packet.RemoteEndPoint, guard.Token);
            await state.LogEntered.Task.WaitAsync(guard.Token);
            caller.Cancel(); state.ReleaseLog.Set();
            var result = await execution.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.Empty(state.Variables);
        }
        finally { state.ReleaseLog.Set(); caller.Cancel(); await execution.WaitAsync(guard.Token); }
    }

    [Fact]
    public async Task Doip_cancel_during_response_log_does_not_publish_or_close_a_completed_transaction()
    {
        using var peer = new TcpListener(IPAddress.Loopback, 0); peer.Start();
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        var plugin = new DoipDiagRequestPlugin();
        var context = DispatchProxy.Create<IExecutionContext, ResponseLogGateProxy>();
        var state = (ResponseLogGateProxy)(object)context;
        state.BlockPrefix = "DoIP 诊断响应:";
        var name = "publication-" + Guid.NewGuid().ToString("N");
        state.SetStep(plugin, new DoipDiagRequestSetting { SessionName = $"\"{name}\"",
            TargetAddress = "\"0x1000\"", RequestData = "\"10 01\"", TimeoutMs = 3000,
            ResultVariable = "Locals.rx" });
        var opening = DoipConnectionManager.ConnectAsync(name, "127.0.0.1", ((IPEndPoint)peer.LocalEndpoint).Port,
            0x0E00, 0, 3000, guard.Token, context.Resources);
        using var socket = await peer.AcceptTcpClientAsync(guard.Token);
        await ReadFrame(socket, guard.Token);
        await SendFrame(socket, 0x0006, [0x0E, 0, 0x10, 0, 0x10, 0, 0, 0, 0], guard.Token);
        var client = await opening;
        var execution = plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
        try
        {
            await ReadFrame(socket, guard.Token);
            await SendFrame(socket, 0x8001, [0x10, 0, 0x0E, 0, 0x50, 1], guard.Token);
            await state.LogEntered.Task.WaitAsync(guard.Token);
            caller.Cancel(); state.ReleaseLog.Set();
            var result = await execution.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.Empty(state.Variables);
            Assert.True(client.IsUsable); // The wire transaction ended before the response logger blocked.
        }
        finally
        {
            state.ReleaseLog.Set(); caller.Cancel();
            await execution.WaitAsync(guard.Token);
            await DoipConnectionManager.CloseAsync(name, guard.Token, context.Resources, 1500);
        }
    }

    private static async Task ReadFrame(TcpClient peer, CancellationToken ct)
    {
        var header = new byte[8]; await peer.GetStream().ReadExactlyAsync(header, ct);
        var body = new byte[BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4))];
        await peer.GetStream().ReadExactlyAsync(body, ct);
    }
    private static async Task SendFrame(TcpClient peer, ushort type, byte[] body, CancellationToken ct)
    {
        var frame = new byte[8 + body.Length]; frame[0] = 2; frame[1] = 0xFD;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), type);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), (uint)body.Length); body.CopyTo(frame, 8);
        await peer.GetStream().WriteAsync(frame, ct);
    }
}

public class ResponseLogGateProxy : TestContext
{
    public string BlockPrefix { get; set; } = "";
    public TaskCompletionSource LogEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ManualResetEventSlim ReleaseLog { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "Log" && args?.Any(x => x is string text && text.StartsWith(BlockPrefix, StringComparison.Ordinal)) == true)
        {
            LogEntered.TrySetResult();
            if (!ReleaseLog.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Controlled response logger was not released");
        }
        return base.Invoke(method, args);
    }
}
