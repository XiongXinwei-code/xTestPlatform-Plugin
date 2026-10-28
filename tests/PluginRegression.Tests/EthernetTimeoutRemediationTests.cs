using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ethernet.DoIP;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

internal static class EthernetTestHostDependencies
{
    // The standalone SDK expression host snapshots loaded references when first created.
    [ModuleInitializer]
    internal static void LoadDefaultExpressionImports() => Assembly.Load("System.Text.Json");
}

public sealed class EthernetTimeoutRemediationTests
{
    [Fact]
    public async Task Doip_step_uses_its_short_request_deadline_instead_of_the_connection_deadline()
    {
        await using var session = await Session.CreateAsync(connectionTimeoutMs: 2000, requestTimeoutMs: 40);
        using var caller = new CancellationTokenSource(15000);
        var execution = session.Plugin.CreateExecutor().ExecuteAsync(session.Context, caller.Token);
        await ReadFrame(session.Peer, caller.Token);
        try
        {
            Assert.Same(execution, await Task.WhenAny(execution, Task.Delay(500, caller.Token)));
            var result = await execution;
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("40", result.StepResult.Error!.Message);
            Assert.NotEmpty(result.StepResult.Error.Detail!);
            Assert.False(session.Client.IsUsable);
        }
        finally { caller.Cancel(); await execution; }
    }

    [Fact]
    public async Task Doip_request_may_outlive_the_short_connection_establishment_budget()
    {
        await using var session = await Session.CreateAsync(connectionTimeoutMs: 150, requestTimeoutMs: 1200);
        using var caller = new CancellationTokenSource(15000);
        var execution = session.Plugin.CreateExecutor().ExecuteAsync(session.Context, caller.Token);
        await ReadFrame(session.Peer, caller.Token);
        await Task.Delay(300, caller.Token);
        try { await SendFrame(session.Peer, 0x8001, [0x10, 0, 0x0E, 0, 0x50, 1], caller.Token); }
        catch (Exception error) when (error is IOException or SocketException) { }
        var result = await execution;
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.Equal("50 01", result.StepResult.Value);
    }

    [Fact]
    public async Task Doip_queue_timeout_does_not_close_the_active_transaction_or_claim_it_was_closed()
    {
        await using var session = await Session.CreateAsync(connectionTimeoutMs: 2000, requestTimeoutMs: 40);
        using var firstCaller = new CancellationTokenSource(15000);
        using var secondCaller = new CancellationTokenSource(15000);
        var first = session.Client.DiagRequestAsync(0x1000, [0x10, 1], firstCaller.Token);
        await ReadFrame(session.Peer, firstCaller.Token);
        var second = session.Plugin.CreateExecutor().ExecuteAsync(session.Context, secondCaller.Token);
        try
        {
            Assert.Same(second, await Task.WhenAny(second, Task.Delay(500, secondCaller.Token)));
            var queued = await second;
            Assert.Equal(TestStatus.Error, queued.StepResult.Status);
            Assert.True(queued.StepResult.Error!.Message.Contains("排队"), queued.StepResult.Error.Detail);
            Assert.DoesNotContain("会话已关闭", queued.StepResult.Error.Message);
            Assert.True(session.Client.IsUsable);
            Assert.False(first.IsCompleted);
            Assert.Equal(0, session.Peer.Available);
            await SendFrame(session.Peer, 0x8001, [0x10, 0, 0x0E, 0, 0x50, 1], firstCaller.Token);
            Assert.Equal(new byte[] { 0x50, 1 }, await first);
        }
        finally
        {
            firstCaller.Cancel(); secondCaller.Cancel();
            try { await first; } catch (OperationCanceledException) { }
            await second;
        }
    }

    [Fact]
    public async Task Doip_per_request_deadline_is_not_restarted_by_ack_or_partial_response()
    {
        await using var session = await Session.CreateAsync(connectionTimeoutMs: 2000, requestTimeoutMs: 70);
        using var caller = new CancellationTokenSource(15000);
        var execution = session.Plugin.CreateExecutor().ExecuteAsync(session.Context, caller.Token);
        await ReadFrame(session.Peer, caller.Token);
        await SendFrame(session.Peer, 0x8002, [0x10, 0, 0x0E, 0, 0], caller.Token);
        // A valid next header followed by an incomplete payload must share the original budget.
        byte[] header = [2, 0xFD, 0x80, 1, 0, 0, 0, 6];
        await session.Peer.GetStream().WriteAsync(header, caller.Token);
        try
        {
            Assert.Same(execution, await Task.WhenAny(execution, Task.Delay(500, caller.Token)));
            var result = await execution;
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("70", result.StepResult.Error!.Message);
            Assert.False(session.Client.IsUsable);
        }
        finally { caller.Cancel(); await execution; }
    }

    [Fact]
    public async Task Doip_caller_cancel_is_aborted_and_does_not_auto_retry()
    {
        await using var session = await Session.CreateAsync(connectionTimeoutMs: 2000, requestTimeoutMs: 1200);
        using var caller = new CancellationTokenSource(15000);
        var execution = session.Plugin.CreateExecutor().ExecuteAsync(session.Context, caller.Token);
        await ReadFrame(session.Peer, caller.Token);
        caller.Cancel();
        var result = await execution;
        Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
        Assert.False(session.Client.IsUsable);
        Assert.Equal(0, session.Peer.Available);
    }

    [Fact]
    public async Task Complete_Doip_negative_response_returns_Error_without_retiring_a_healthy_session()
    {
        await using var session = await Session.CreateAsync(connectionTimeoutMs: 2000, requestTimeoutMs: 1200);
        using var guard = new CancellationTokenSource(10000);
        var first = session.Plugin.CreateExecutor().ExecuteAsync(session.Context, guard.Token);
        await ReadFrame(session.Peer, guard.Token);
        await SendFrame(session.Peer, 0x8001, [0x10, 0, 0x0E, 0, 0x7F, 0x10, 0x22], guard.Token);
        Assert.Equal(TestStatus.Error, (await first.WaitAsync(guard.Token)).StepResult.Status);
        Assert.True(session.Client.IsUsable);
        var next = session.Plugin.CreateExecutor().ExecuteAsync(session.Context, guard.Token);
        await ReadFrame(session.Peer, guard.Token);
        await SendFrame(session.Peer, 0x8001, [0x10, 0, 0x0E, 0, 0x50, 0x01], guard.Token);
        Assert.Equal(TestStatus.Completed, (await next.WaitAsync(guard.Token)).StepResult.Status);
        Assert.True(session.Client.IsUsable);
    }

    private static async Task<byte[]> ReadFrame(TcpClient peer, CancellationToken ct)
    {
        var header = new byte[8];
        await peer.GetStream().ReadExactlyAsync(header, ct);
        var data = new byte[BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4))];
        await peer.GetStream().ReadExactlyAsync(data, ct);
        return data;
    }

    private static async Task SendFrame(TcpClient peer, ushort type, byte[] data, CancellationToken ct)
    {
        var frame = new byte[8 + data.Length];
        frame[0] = 2; frame[1] = 0xFD;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), type);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), (uint)data.Length);
        data.CopyTo(frame, 8);
        await peer.GetStream().WriteAsync(frame, ct);
    }

    private sealed class Session : IAsyncDisposable
    {
        public Ethernet.DoIP.DoipDiagRequestPlugin Plugin { get; } = new();
        public IExecutionContext Context { get; private set; } = null!;
        public TcpClient Peer { get; private set; } = null!;
        public DoipClient Client { get; private set; } = null!;
        private readonly string _name = "deadline-" + Guid.NewGuid().ToString("N");

        public static async Task<Session> CreateAsync(int connectionTimeoutMs, int requestTimeoutMs)
        {
            var session = new Session();
            (session.Context, _) = TestContext.Create(session.Plugin, new DoipDiagRequestSetting
            {
                SessionName = $"\"{session._name}\"", TargetAddress = "\"0x1000\"",
                RequestData = "\"10 01\"", TimeoutMs = requestTimeoutMs
            });
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var guard = new CancellationTokenSource(15000);
            var opening = DoipConnectionManager.ConnectAsync(session._name, "127.0.0.1",
                ((IPEndPoint)listener.LocalEndpoint).Port, 0x0E00, 0, connectionTimeoutMs, guard.Token, session.Context.Resources);
            session.Peer = await listener.AcceptTcpClientAsync(guard.Token);
            await ReadFrame(session.Peer, guard.Token);
            await SendFrame(session.Peer, 0x0006, [0x0E, 0, 0x10, 0, 0x10, 0, 0, 0, 0], guard.Token);
            session.Client = await opening;
            return session;
        }

        public async ValueTask DisposeAsync()
        {
            try { await DoipConnectionManager.CloseAsync(_name, default, Context.Resources, 1500); }
            finally { Peer.Dispose(); }
        }
    }
}
