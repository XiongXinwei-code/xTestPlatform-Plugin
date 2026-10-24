using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Ethernet;
using Ethernet.Models;
using Ethernet.Runtime;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

// Exercises the real SDK registry, not the platform's unrun debug-window UI.
// Devices are replaced with gated handles/HTTP handlers; TCP uses loopback only.
public sealed class Round2SdkModeTransitionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_during_a_noncooperative_open_keeps_the_first_reopen_waiting_for_the_exact_late_handle(bool newDebugContext)
    {
        using var guard = new CancellationTokenSource(10000);
        var registry = SdkResourceLifecycleTests.NewRegistry();
        var nextRegistry = newDebugContext ? SdkResourceLifecycleTests.NewRegistry() : registry;
        string name = "round2-opening-" + Guid.NewGuid().ToString("N");
        var pool = NamedConnectionPool<GatedHandle>.For(registry, "round2.pool", new());
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var openingExit = new TaskCompletionSource<GatedHandle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseDispose = new ManualResetEventSlim();
        var old = new GatedHandle(() =>
        {
            disposing.TrySetResult();
            if (!releaseDispose.Wait(10000)) throw new TimeoutException("test dispose barrier");
        });
        var first = pool.OpenAsync(name, _ => { opened.TrySetResult(); return openingExit.Task; }, 5000, guard.Token);
        await opened.Task.WaitAsync(guard.Token);
        var clock = Stopwatch.StartNew();
        SdkResourceLifecycleTests.Cleanup(registry, newDebugContext ? "CleanupAll" : "CleanupExecution");
        Assert.True(clock.ElapsedMilliseconds < 1000, "SDK cleanup must return without waiting for native open/release");
        var next = NamedConnectionPool<GatedHandle>.For(nextRegistry, "round2.pool", new());
        int nextFactoryCalls = 0;
        var current = new GatedHandle(() => { });
        var reopening = next.OpenAsync(name, _ =>
        {
            Interlocked.Increment(ref nextFactoryCalls);
            return Task.FromResult(current);
        }, 5000, guard.Token);
        try
        {
            Assert.False(reopening.IsCompleted);
            Assert.Equal(0, nextFactoryCalls);
            openingExit.TrySetResult(old);
            await disposing.Task.WaitAsync(guard.Token);
            Assert.False(reopening.IsCompleted);
            Assert.Equal(0, nextFactoryCalls);
            releaseDispose.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(guard.Token));
            Assert.Same(current, await reopening.WaitAsync(guard.Token));
            Assert.Equal(1, nextFactoryCalls);
            Assert.Equal(1, old.Releases);
            Assert.Same(current, next.Get(name));
            Assert.Throws<ObjectDisposedException>(() => pool.Get(name));
        }
        finally
        {
            releaseDispose.Set();
            openingExit.TrySetResult(old);
            try { await first.WaitAsync(guard.Token); } catch (OperationCanceledException) { }
            await reopening.WaitAsync(guard.Token);
            await next.CloseAsync(name, 1000, guard.Token);
            SdkResourceLifecycleTests.Cleanup(nextRegistry, "CleanupAll");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_tcp_receive_is_interrupted_by_sdk_cleanup_and_the_first_new_execution_receives_only_new_data(bool newDebugContext)
    {
        using var guard = new CancellationTokenSource(10000);
        using var peer = new TcpListener(IPAddress.Loopback, 0);
        peer.Start();
        var registry = SdkResourceLifecycleTests.NewRegistry();
        var ctx = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)ctx;
        state.Registry = registry;
        string name = "round2-tcp-" + Guid.NewGuid().ToString("N");
        var open = new TcpOpenPlugin();
        var openSetting = new TcpOpenSetting
        {
            ConnectionName = $"\"{name}\"", RemoteHost = "\"127.0.0.1\"",
            RemotePort = ((IPEndPoint)peer.LocalEndpoint).Port.ToString(), ConnectTimeoutMs = 2000
        };
        state.SetStep(open, openSetting);
        var firstOpen = open.CreateExecutor().ExecuteAsync(ctx, guard.Token);
        using var oldPeer = await peer.AcceptTcpClientAsync(guard.Token);
        Assert.Equal(TestStatus.Completed, (await firstOpen.WaitAsync(guard.Token)).StepResult.Status);
        var read = new TcpReceivePlugin();
        var readSetting = new TcpReceiveSetting
        {
            ConnectionName = $"\"{name}\"", TimeoutMs = 5000,
            ExpectedLength = 1, ResultVariable = "Locals.received"
        };
        state.SetStep(read, readSetting);
        var oldRead = read.CreateExecutor().ExecuteAsync(ctx, guard.Token);
        Assert.False(oldRead.IsCompleted);
        SdkResourceLifecycleTests.Cleanup(registry, newDebugContext ? "CleanupAll" : "CleanupExecution");
        if (newDebugContext) state.Registry = SdkResourceLifecycleTests.NewRegistry();
        try
        {
            state.SetStep(open, openSetting);
            var newOpen = open.CreateExecutor().ExecuteAsync(ctx, guard.Token);
            using var currentPeer = await peer.AcceptTcpClientAsync(guard.Token);
            Assert.Equal(TestStatus.Completed, (await newOpen.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(TestStatus.Error, (await oldRead.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Empty(state.Variables);
            state.SetStep(read, readSetting);
            var newRead = read.CreateExecutor().ExecuteAsync(ctx, guard.Token);
            await currentPeer.GetStream().WriteAsync(new byte[] { 0x5A }, guard.Token);
            var result = await newRead.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.Equal("5A", state.Variables["Locals.received"]);
            Assert.Equal("5A", result.StepResult.Value);
        }
        finally
        {
            SdkResourceLifecycleTests.Cleanup(state.Registry, "CleanupAll");
            await oldRead.WaitAsync(guard.Token);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Http_sdk_cleanup_keeps_old_native_request_owned_without_blocking_the_first_new_request_or_overwriting_its_result(bool newDebugContext)
    {
        using var guard = new CancellationTokenSource(10000);
        using var oldCaller = new CancellationTokenSource();
        var registry = SdkResourceLifecycleTests.NewRegistry();
        var ctx = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)ctx;
        state.Registry = registry;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new HttpClientResource
        {
            Client = new HttpClient(new Handler(_ => { entered.TrySetResult(); return lateResponse.Task; }))
            { BaseAddress = new("http://old.invalid/"), Timeout = TimeSpan.FromSeconds(5) }
        };
        registry.Set("Http.Client.Mes", old);
        var read = new HttpRequestPlugin();
        state.SetStep(read, new HttpRequestSetting { Path = "\"read\"", ResponseVariable = "Locals.received" });
        var oldRead = read.CreateExecutor().ExecuteAsync(ctx, oldCaller.Token);
        await entered.Task.WaitAsync(guard.Token);
        var clock = Stopwatch.StartNew();
        SdkResourceLifecycleTests.Cleanup(registry, newDebugContext ? "CleanupAll" : "CleanupExecution");
        Assert.True(clock.ElapsedMilliseconds < 1000);
        if (newDebugContext) state.Registry = SdkResourceLifecycleTests.NewRegistry();
        var current = new HttpClientResource
        {
            Client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("new-result") }))) { BaseAddress = new("http://new.invalid/") }
        };
        state.Registry.Set("Http.Client.Mes", current);
        try
        {
            // Do not wait for the old foreground waiter or its request timeout before
            // proving that the first new execution can actually complete its request.
            Assert.False(oldRead.IsCompleted);
            Assert.False(old.IsClosed);
            Assert.Empty(state.Variables);
            Assert.Equal(TestStatus.Completed, (await read.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
            Assert.Equal("new-result", state.Variables["Locals.received"]);
            Assert.False(oldRead.IsCompleted);
            oldCaller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await oldRead.WaitAsync(guard.Token)).StepResult.Status);
            await Assert.ThrowsAsync<TimeoutException>(() => old.CloseAsync(50, guard.Token));
            lateResponse.TrySetResult(new(HttpStatusCode.OK) { Content = new StringContent("old-result") });
            await old.CloseAsync(1000, guard.Token);
            Assert.True(old.IsClosed);
            Assert.Equal("new-result", state.Variables["Locals.received"]);
            Assert.True(state.Registry.TryGet<HttpClientResource>("Http.Client.Mes", out var registered));
            Assert.Same(current, registered);
        }
        finally
        {
            oldCaller.Cancel();
            lateResponse.TrySetResult(new(HttpStatusCode.OK) { Content = new StringContent("old-result") });
            await oldRead.WaitAsync(guard.Token);
            await old.CloseAsync(1000, guard.Token);
            await current.CloseAsync(1000, guard.Token);
            SdkResourceLifecycleTests.Cleanup(state.Registry, "CleanupAll");
        }
    }

    private sealed class GatedHandle(Action release) : IDisposable
    {
        internal int Releases;
        public void Dispose() { release(); Interlocked.Increment(ref Releases); }
    }

    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(ct);
    }
}
