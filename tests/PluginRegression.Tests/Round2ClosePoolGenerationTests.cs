using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Ethernet;
using Ethernet.DoIP;
using Ethernet.DoIP.Models;
using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class Round2ClosePoolGenerationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_close_that_resolves_its_pool_after_sdk_cleanup_must_not_close_the_new_execution_pool(bool doip)
    {
        using var guard = new CancellationTokenSource(10000);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string name = "round2-close-" + Guid.NewGuid().ToString("N");
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var peers = new List<TcpClient>();
        async Task<object> Open()
        {
            if (!doip)
            {
                var opening = TcpConnectionManager.ConnectAsync(name, "127.0.0.1", port, 2000, guard.Token, resources);
                peers.Add(await listener.AcceptTcpClientAsync(guard.Token));
                return await opening.WaitAsync(guard.Token);
            }
            var routing = DoipConnectionManager.ConnectAsync(name, "127.0.0.1", port, 0x0E00, 0, 2000, guard.Token, resources);
            var peer = await listener.AcceptTcpClientAsync(guard.Token); peers.Add(peer);
            var request = new byte[15];
            await peer.GetStream().ReadExactlyAsync(request, guard.Token);
            Assert.Equal(new byte[] { 0x02, 0xFD, 0, 5 }, request[..4]);
            await peer.GetStream().WriteAsync(new byte[] { 2, 0xFD, 0, 6, 0, 0, 0, 9, 0x0E, 0, 0x12, 0x34, 0x10, 0, 0, 0, 0 }, guard.Token);
            return await routing.WaitAsync(guard.Token);
        }
        _ = await Open();
        var observing = DispatchProxy.Create<IResourceRegistry, PoolLookupBarrier>();
        var barrier = (PoolLookupBarrier)(object)observing;
        barrier.Inner = resources;
        barrier.Key = doip ? "Ethernet.DoIP.ConnectionPool" : "Ethernet.TCP.ConnectionPool";
        var ctx = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)ctx; state.Registry = observing;
        IStepPlugin close = doip ? new DoipDisconnectPlugin() : new TcpClosePlugin();
        state.SetStep(close, doip ? new DoipDisconnectSetting { SessionName = $"\"{name}\"", TimeoutMs = 2000 }
            : new TcpCloseSetting { ConnectionName = $"\"{name}\"", TimeoutMs = 2000 });
        var closing = Task.Run(() => close.CreateExecutor().ExecuteAsync(ctx, guard.Token));
        try
        {
            await barrier.Entered.Task.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(resources, "CleanupExecution");
            var current = await Open();
            barrier.Release.Set();
            Assert.Equal(TestStatus.Error, (await closing.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Same(current, doip ? (object)DoipConnectionManager.Get(name, resources) : TcpConnectionManager.Get(name, resources));
        }
        finally
        {
            barrier.Release.Set();
            await closing.WaitAsync(guard.Token);
            if (doip) await DoipConnectionManager.CloseAsync(name, guard.Token, resources, 1000);
            else await TcpConnectionManager.CloseAsync(name, guard.Token, resources, 1000);
            SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll");
            foreach (var peer in peers) peer.Dispose();
        }
    }
}

public class PoolLookupBarrier : DispatchProxy
{
    public IResourceRegistry Inner { get; set; } = null!;
    public string Key { get; set; } = "";
    public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly ManualResetEventSlim Release = new();
    private int _observed;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet" && (string?)args?[0] == Key && Interlocked.Exchange(ref _observed, 1) == 0)
        {
            Entered.TrySetResult();
            if (!Release.Wait(10000)) throw new TimeoutException("test pool lookup barrier");
        }
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
