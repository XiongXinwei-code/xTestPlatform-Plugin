extern alias EthernetCore;

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using Ethernet;
using Ethernet.Models;
using Ethernet.Executors;
using Ethernet.DoIP;
using Ethernet.DoIP.Models;
using Ethernet.Runtime;
using Ethernet.SomeIP;
using Ethernet.SomeIP.Models;
using Ethernet.SomeIP.Executors;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;
using ExecutionEpoch = EthernetCore::PluginCommon.ExecutionEpoch;

namespace PluginRegression.Tests;

// Real SDK resource cleanup and localhost sockets; no user device or platform UI is used.
public sealed class FollowupTransientNetworkLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_finished_throwing_cancel_callback_does_not_permanently_reserve_a_native_resource_after_actual_io_exit(bool releaseAlsoFails)
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var epoch = ExecutionEpoch.Capture(resources, "Ethernet");
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        var native = new NativeProbe();
        if (releaseAlsoFails) native.DisposeFailure = new InvalidOperationException("native-dispose-error");
        using var owner = await TransientNetworkOperation<NativeProbe>.OpenAsync(resources,
            () => native, port, 2000, default, epoch.ThrowIfExpired);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ioRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = owner.RunAsync(async (handle, token) =>
        {
            _ = token.Register(() => { callbackEntered.TrySetResult(); throw new InvalidOperationException("native-cancel-callback-error"); });
            entered.TrySetResult();
            await ioRelease.Task;
            return 1;
        }, default, epoch.ThrowIfExpired);
        var nextResources = SdkResourceLifecycleTests.NewRegistry();
        Task<TransientNetworkOperation<NativeProbe>>? reopening = null;
        int factories = 0;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll");
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<InvalidOperationException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(1)));
            reopening = TransientNetworkOperation<NativeProbe>.OpenAsync(nextResources,
                () => { Interlocked.Increment(ref factories); return new NativeProbe(); }, port, 2500, default, () => { });
            await Task.Delay(30);
            Assert.False(reopening.IsCompleted);
            Assert.Equal(0, native.Releases);
            ioRelease.TrySetResult();
            if (releaseAlsoFails)
            {
                var releaseError = await Assert.ThrowsAsync<InvalidOperationException>(() => reopening);
                Assert.Contains("native-cancel-callback-error", releaseError.ToString());
                Assert.Contains("native-dispose-error", releaseError.ToString());
                Assert.Equal(0, factories);
                Assert.Equal(0, native.Releases);
                native.DisposeFailure = null;
                reopening = TransientNetworkOperation<NativeProbe>.OpenAsync(nextResources,
                    () => { Interlocked.Increment(ref factories); return new NativeProbe(); }, port, 2500, default, () => { });
            }
            using var current = await reopening.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1, native.Releases);
            Assert.Equal(1, factories);
            Assert.Contains("native-cancel-callback-error", owner.CancellationFailure!.ToString());
        }
        finally
        {
            ioRelease.TrySetResult();
            try { await waiting; } catch (InvalidOperationException) { }
            if (reopening is not null)
                try { (await reopening).Dispose(); } catch (Exception) { /* RED retains the fake failed owner; no native device exists. */ }
            SdkResourceLifecycleTests.Cleanup(nextResources, "CleanupAll");
        }
    }

    [Fact]
    public async Task Original_caller_cancellation_does_not_wait_for_blocked_native_cancel_callbacks()
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var epoch = ExecutionEpoch.Capture(resources, "Ethernet");
        using var caller = new CancellationTokenSource();
        var native = new NativeProbe();
        using var owner = await TransientNetworkOperation<NativeProbe>.OpenAsync(resources,
            () => native, 0, 3000, caller.Token, epoch.ThrowIfExpired);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ioRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callbackRelease = new ManualResetEventSlim();
        var waiting = owner.RunAsync(async (handle, token) =>
        {
            _ = token.Register(() =>
            {
                callbackEntered.TrySetResult();
                if (!callbackRelease.Wait(10000)) throw new TimeoutException("test native callback barrier");
            });
            entered.TrySetResult();
            await ioRelease.Task; // actual driver deliberately remains active after caller cancellation.
            return 1;
        }, caller.Token, epoch.ThrowIfExpired);
        Task? cancelling = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancelling = caller.CancelAsync();
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(0, native.Releases);
            owner.Dispose();
            Assert.Equal(0, native.Releases);
        }
        finally
        {
            ioRelease.TrySetResult(); callbackRelease.Set();
            if (cancelling is not null) await cancelling.WaitAsync(TimeSpan.FromSeconds(5));
            try { await waiting; } catch (OperationCanceledException) { }
            SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Doip_discovery_receiver_exits_on_sdk_cleanup_and_the_first_new_run_can_discover_again(bool newDebugRegistry)
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var context = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)context;
        state.Registry = resources;
        var plugin = new DoipVehicleDiscoveryPlugin();
        var setting = new DoipVehicleDiscoverySetting
        { BroadcastAddress = "\"127.0.0.1\"", Port = ((IPEndPoint)peer.Client.LocalEndPoint!).Port, TimeoutMs = 5000, ResultVariable = "Locals.Vehicle" };
        state.SetStep(plugin, setting);
        state.DeclaredVariables["Locals.Vehicle"] = "";
        using var guard = new CancellationTokenSource(15000);
        var oldRun = plugin.CreateExecutor().ExecuteAsync(context, CancellationToken.None);
        var oldRequest = await peer.ReceiveAsync(guard.Token);
        Assert.Equal(8, oldRequest.Buffer.Length);
        SdkResourceLifecycleTests.Cleanup(resources, newDebugRegistry ? "CleanupAll" : "CleanupExecution");
        var nextResources = newDebugRegistry ? SdkResourceLifecycleTests.NewRegistry() : resources;
        var nextContext = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var nextState = (SdkContext)(object)nextContext;
        nextState.Registry = nextResources;
        nextState.SetStep(plugin, setting);
        nextState.DeclaredVariables["Locals.Vehicle"] = "";
        Task<ExecutionResult>? rerun = null;
        try
        {
            Assert.Equal(TestStatus.Error, (await oldRun.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.Empty(state.Variables);
            while (IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(endpoint => endpoint.Port == oldRequest.RemoteEndPoint.Port))
                await Task.Delay(10, guard.Token);
            rerun = plugin.CreateExecutor().ExecuteAsync(nextContext, CancellationToken.None);
            var nextRequest = await peer.ReceiveAsync(guard.Token);
            var response = new byte[40];
            response[0] = 2; response[1] = 0xFD; response[3] = 4; response[7] = 32;
            System.Text.Encoding.ASCII.GetBytes("LOCALHOSTVIN00001").CopyTo(response, 8);
            response[25] = 0x12; response[26] = 0x34;
            await peer.SendAsync(response, nextRequest.RemoteEndPoint, guard.Token);
            Assert.Equal(TestStatus.Completed, (await rerun.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Contains("LogicalAddress=0x1234", nextState.Variables["Locals.Vehicle"]!.ToString());
        }
        finally
        {
            // A valid announcement also releases a legacy implementation if this probe regresses.
            await peer.SendAsync(new byte[40], oldRequest.RemoteEndPoint, guard.Token);
            await oldRun.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(nextResources, "CleanupAll");
            if (rerun is not null) await rerun.WaitAsync(guard.Token);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_temporary_owner_keeps_actual_io_and_cancel_callbacks_until_they_exit(bool blockingCallback)
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var epoch = ExecutionEpoch.Capture(resources, "Ethernet");
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        var old = new NativeProbe();
        using var owner = await TransientNetworkOperation<NativeProbe>.OpenAsync(resources, () => old,
            port, 2000, CancellationToken.None, epoch.ThrowIfExpired);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actualExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseIo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCallback = new ManualResetEventSlim();
        var operation = owner.RunAsync(async (native, token) =>
        {
            _ = token.Register(() =>
            {
                callbackEntered.TrySetResult();
                if (blockingCallback && !releaseCallback.Wait(10000)) throw new TimeoutException("test callback barrier");
            });
            entered.TrySetResult();
            try { await releaseIo.Task; return 42; } // Deliberately ignores cancellation: native exit must be retained.
            finally { actualExited.TrySetResult(); }
        }, CancellationToken.None, epoch.ThrowIfExpired);
        Task<TransientNetworkOperation<NativeProbe>>? reopening = null;
        var nextResources = SdkResourceLifecycleTests.NewRegistry();
        int factoryCalls = 0;
        var current = new NativeProbe();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stopwatch = Stopwatch.StartNew();
            SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll");
            Assert.True(stopwatch.ElapsedMilliseconds < 1000);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<InvalidOperationException>(() => operation.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(0, old.Releases);
            reopening = TransientNetworkOperation<NativeProbe>.OpenAsync(nextResources,
                () => { Interlocked.Increment(ref factoryCalls); return current; }, port, 3000,
                CancellationToken.None, () => { });
            await Task.Delay(30);
            Assert.False(reopening.IsCompleted);
            Assert.Equal(0, factoryCalls);
            releaseIo.TrySetResult();
            await actualExited.Task.WaitAsync(TimeSpan.FromSeconds(1));
            if (blockingCallback)
            {
                Assert.False(reopening.IsCompleted);
                Assert.Equal(0, old.Releases);
            }
            releaseCallback.Set();
            using var next = await reopening.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1, old.Releases);
            Assert.Equal(1, factoryCalls);
        }
        finally
        {
            releaseIo.TrySetResult(); releaseCallback.Set();
            try { await operation; } catch (InvalidOperationException) { }
            if (reopening is not null) (await reopening).Dispose();
            SdkResourceLifecycleTests.Cleanup(nextResources, "CleanupAll");
        }
    }

    [Fact]
    public async Task A_delayed_temporary_finally_cannot_close_the_replacement_handle()
    {
        var pool = new NamedConnectionPool<NativeProbe>();
        string name = Guid.NewGuid().ToString("N");
        var old = await pool.OpenAsync(name, _ => Task.FromResult(new NativeProbe()), 2000, default);
        var current = await pool.OpenAsync(name, _ => Task.FromResult(new NativeProbe()), 2000, default);
        pool.RequestCloseOwned(name, old);
        Assert.Same(current, pool.Get(name));
        Assert.Equal(1, old.Releases);
        Assert.Equal(0, current.Releases);
        await pool.CloseAsync(name, 2000, default);
        Assert.Equal(1, current.Releases);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Cleanup_cancels_temporary_someip_dns_or_tcp_connect_without_cancelling_the_caller(bool noResponse, bool tcp)
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var context = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)context;
        state.Registry = resources;
        IStepPlugin plugin = noResponse ? new SomeIpFireAndForgetPlugin() : new SomeIpRequestPlugin();
        object setting = noResponse
            ? new SomeIpFireAndForgetSetting { RemoteHost = "\"controlled.invalid\"", Transport = tcp ? SomeIpTransport.Tcp : SomeIpTransport.Udp, SendTimeoutMs = 5000 }
            : new SomeIpRequestSetting { RemoteHost = "\"controlled.invalid\"", Transport = tcp ? SomeIpTransport.Tcp : SomeIpTransport.Udp, TimeoutMs = 5000 };
        state.SetStep(plugin, setting);
        var network = new BlockedNetwork();
        IStepExecutor executor = noResponse ? new SomeIpFireAndForgetExecutor(network) : new SomeIpRequestExecutor(network);
        var pending = executor.ExecuteAsync(context, CancellationToken.None);
        try
        {
            await network.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            SdkResourceLifecycleTests.Cleanup(resources, "CleanupExecution");
            await network.Exited.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(TestStatus.Error, (await pending.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.Equal(0, network.Sends);
            Assert.Empty(state.Variables);
        }
        finally
        {
            network.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(6));
            SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll");
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Cleanup_without_caller_cancellation_releases_the_original_listener_for_first_rerun(
        bool someIp, bool newDebugRegistry)
    {
        using var portReservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)portReservation.Client.LocalEndPoint!).Port;
        portReservation.Dispose();
        var resources = SdkResourceLifecycleTests.NewRegistry();
        IStepPlugin plugin = someIp ? new SomeIpSubscribePlugin() : new UdpReceivePlugin();
        object setting = someIp
            ? new SomeIpSubscribeSetting { LocalPort = $"\"{port}\"", TimeoutMs = 5000, ResultVariable = "Locals.Data" }
            : new UdpReceiveSetting { LocalPort = port, TimeoutMs = 5000, ResultVariable = "Locals.Data" };
        var oldContext = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var oldState = (SdkContext)(object)oldContext;
        oldState.Registry = resources;
        oldState.SetStep(plugin, setting);
        oldState.DeclaredVariables["Locals.Data"] = "";
        // Warm exactly the expression literals used by this listener; this probe targets
        // receive ownership rather than first-use Roslyn initialization.
        if (someIp)
        {
            await EthernetExecutorHelper.EvalStringAsync($"\"{port}\"", oldContext);
            await EthernetExecutorHelper.EvalStringAsync("\"0x1234\"", oldContext);
            await EthernetExecutorHelper.EvalStringAsync("\"0x8001\"", oldContext);
        }
        var oldRun = plugin.CreateExecutor().ExecuteAsync(oldContext, CancellationToken.None);
        await WaitUntilPortBound(port, oldRun);
        var cleanup = Stopwatch.StartNew();
        SdkResourceLifecycleTests.Cleanup(resources, newDebugRegistry ? "CleanupAll" : "CleanupExecution");
        Assert.True(cleanup.ElapsedMilliseconds < 1000, "SDK cleanup must not block on network I/O or socket release");
        var nextResources = newDebugRegistry ? SdkResourceLifecycleTests.NewRegistry() : resources;
        var nextContext = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var nextState = (SdkContext)(object)nextContext;
        nextState.Registry = nextResources;
        nextState.SetStep(plugin, setting);
        nextState.DeclaredVariables["Locals.Data"] = "";
        Task<ExecutionResult>? rerun = null;
        using var sender = new UdpClient();
        var payload = someIp ? new SomeIpMessage
        {
            ServiceId = 0x1234, MethodId = 0x8001,
            MessageType = SomeIpMessageType.Notification, Payload = [0x5A]
        }.Encode() : new byte[] { 0x5A };
        try
        {
            rerun = plugin.CreateExecutor().ExecuteAsync(nextContext, CancellationToken.None);
            using var limit = new CancellationTokenSource(8000);
            while (!rerun.IsCompleted)
            {
                await sender.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, port), limit.Token);
                await Task.Delay(20, limit.Token);
            }
            var result = await rerun;
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.Equal("5A", nextState.Variables["Locals.Data"]);
            Assert.Equal(TestStatus.Error, (await oldRun.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.False(oldState.Variables.ContainsKey("Locals.Data"));
        }
        finally
        {
            // Unblock the old unregistered implementation during RED without leaving a socket behind.
            await sender.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, port));
            await oldRun.WaitAsync(TimeSpan.FromSeconds(6));
            if (rerun is not null) await rerun.WaitAsync(TimeSpan.FromSeconds(6));
            SdkResourceLifecycleTests.Cleanup(nextResources, "CleanupAll");
        }
    }

    private static async Task WaitUntilPortBound(int port, Task<ExecutionResult> pending)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < 15000)
        {
            Assert.False(pending.IsCompleted, "listener exited before entering native receive");
            if (IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(endpoint => endpoint.Port == port)) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("test listener never acquired its localhost port");
    }

    private sealed class BlockedNetwork : IEthernetNetworkIo
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Sends;
        private async Task WaitAsync(CancellationToken ct)
        {
            Entered.TrySetResult();
            try { await Release.Task.WaitAsync(ct); }
            finally { Exited.TrySetResult(); }
        }
        public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
        { await WaitAsync(ct); return [IPAddress.Loopback]; }
        public Task ConnectTcpAsync(TcpClient client, string host, int port, CancellationToken ct) => WaitAsync(ct);
        public Task WriteTcpAsync(NetworkStream stream, byte[] data, CancellationToken ct)
        { Interlocked.Increment(ref Sends); return Task.CompletedTask; }
        public Task SendUdpAsync(UdpClient client, IPEndPoint endpoint, byte[] data, CancellationToken ct)
        { Interlocked.Increment(ref Sends); return Task.CompletedTask; }
    }

    private sealed class NativeProbe : IDisposable
    {
        internal int Releases;
        internal Exception? DisposeFailure;
        public void Dispose()
        {
            if (DisposeFailure is not null) throw DisposeFailure;
            Interlocked.Increment(ref Releases);
        }
    }
}
