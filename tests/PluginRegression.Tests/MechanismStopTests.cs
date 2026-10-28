extern alias HttpCore;
extern alias EthernetCore;
using System.Net.Http;
using Ethernet.Runtime;
using Http.Helpers;
using HttpEpoch = HttpCore::PluginCommon.ExecutionEpoch;
using EthernetScope = EthernetCore::PluginCommon.ExecutionScopedExecutor;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class MechanismStopTests
{
    [Fact]
    public async Task Cancelling_one_open_on_a_reused_pool_does_not_close_its_other_active_connection()
    {
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        var registry = SdkResourceLifecycleTests.NewRegistry();
        var context = System.Reflection.DispatchProxy.Create<xTestPlatform.Core.Engine.IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)context; state.Registry = registry;
        var plugin = new Ethernet.TcpOpenPlugin(); state.SetStep(plugin, new Ethernet.Models.TcpOpenSetting());
        var pool = NamedConnectionPool<Handle>.For(registry, "mechanism-owned-pool", new());
        var keep = new Handle(); await pool.OpenAsync("keep", _ => Task.FromResult(keep), 1000, default);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new Handle();
        var execution = EthernetScope.Create("Ethernet", () => new PoolOpeningProbe(async ct =>
        {
            await pool.OpenAsync("new", _ => { entered.TrySetResult(); return late.Task; }, 30000, ct);
            return new() { StepResult = new() { Status = TestStatus.Completed } };
        })).ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(guard.Token); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.Same(keep, pool.Get("keep")); Assert.Equal(0, keep.Releases); Assert.Equal(0, pending.Releases);
            late.TrySetResult(pending);
            await pool.CloseAsync("new", 2000, guard.Token);
            Assert.Equal(1, pending.Releases); Assert.Same(keep, pool.Get("keep"));
        }
        finally { late.TrySetResult(pending); await pool.CloseAsync("new", 2000, guard.Token); await pool.CloseAsync("keep", 2000, guard.Token); pool.Dispose(); SdkResourceLifecycleTests.Cleanup(registry, "CleanupAll"); }
    }

    [Theory]
    [InlineData("dispose")]
    [InlineData("close")]
    [InlineData("replace")]
    public async Task Noncooperative_factory_keeps_actual_handle_ownership_but_does_not_hold_retired_open_waiter(string stopKind)
    {
        using var guard = new CancellationTokenSource(10000);
        var pool = new NamedConnectionPool<Handle>();
        string name = Guid.NewGuid().ToString("N");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new Handle(); var current = new Handle();
        var first = pool.OpenAsync(name, _ => { entered.TrySetResult(); return late.Task; }, 30000, default);
        Task? stopping = null; Task<Handle>? replacement = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            if (stopKind == "dispose") pool.Dispose();
            else if (stopKind == "close") stopping = pool.CloseAsync(name, 5000, default);
            else replacement = pool.OpenAsync(name, _ => Task.FromResult(current), 5000, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(0, old.Releases);
            if (stopping != null) Assert.False(stopping.IsCompleted);
            if (replacement != null) Assert.False(replacement.IsCompleted);
            late.TrySetResult(old);
            if (stopping != null) await stopping.WaitAsync(guard.Token);
            if (replacement != null) Assert.Same(current, await replacement.WaitAsync(guard.Token));
            while (Volatile.Read(ref old.Releases) != 1) await Task.Delay(1, guard.Token);
            Assert.Equal(1, old.Releases);
            if (replacement != null) Assert.Same(current, pool.Get(name));
        }
        finally
        {
            late.TrySetResult(old);
            try { await first.WaitAsync(guard.Token); } catch (InvalidOperationException) { }
            if (replacement != null) await replacement.WaitAsync(guard.Token);
            if (stopping != null) await stopping.WaitAsync(guard.Token);
            if (stopKind != "dispose") await pool.CloseAsync(name, 2000, guard.Token);
            pool.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Http_builder_retirement_releases_frontend_and_epoch_gate_without_discarding_its_exact_late_client(bool all)
    {
        using var guard = new CancellationTokenSource(10000);
        using var release = new ManualResetEventSlim();
        var registry = SdkResourceLifecycleTests.NewRegistry();
        var epoch = HttpEpoch.Capture(registry, "Http");
        var oldGate = HttpHelper.GetRegistryGate(registry);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var building = HttpHelper.BuildClientAsync(() =>
        {
            entered.TrySetResult();
            if (!release.Wait(10000)) throw new TimeoutException("test builder barrier");
            return new HttpClient(new Handler(() => disposed.TrySetResult()));
        }, 0, default, epoch.StopSignal);
        HttpClient? next = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(registry, all ? "CleanupAll" : "CleanupExecution");
            await Assert.ThrowsAsync<InvalidOperationException>(() => building.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(disposed.Task.IsCompleted);
            Assert.NotSame(oldGate, HttpHelper.GetRegistryGate(registry));
            next = await HttpHelper.BuildClientAsync(() => new HttpClient(), 1000, default,
                HttpEpoch.Capture(registry, "Http").StopSignal);
            release.Set();
            await disposed.Task.WaitAsync(guard.Token);
            HttpEpoch.Capture(registry, "Http").ThrowIfExpired();
        }
        finally
        {
            release.Set(); next?.Dispose();
            try { await building.WaitAsync(guard.Token); } catch (InvalidOperationException) { }
            await disposed.Task.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(registry, "CleanupAll");
        }
    }

    private sealed class Handle : IDisposable { internal int Releases; public void Dispose() => Interlocked.Increment(ref Releases); }
    private sealed class Handler(Action dispose) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) dispose(); base.Dispose(disposing); }
    }
    private sealed class PoolOpeningProbe(Func<CancellationToken, Task<ExecutionResult>> execute) : xTestPlatform.Core.Plugins.Contracts.IStepExecutor
    {
        public Task<ExecutionResult> ExecuteAsync(xTestPlatform.Core.Engine.IExecutionContext context, CancellationToken cancellationToken = default) => execute(cancellationToken);
    }
}
