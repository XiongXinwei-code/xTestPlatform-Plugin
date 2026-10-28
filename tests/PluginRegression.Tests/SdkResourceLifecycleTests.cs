using System.Diagnostics;
using System.Reflection;
using Ethernet.Runtime;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

// Real SDK1.2.8 registry, fake native handle. These are not real debug-window E2E tests.
public sealed class SdkResourceLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execution_cleanup_and_new_debug_registry_allow_the_first_reopen_only_after_actual_old_release(bool newDebugRegistry)
    {
        var resources = NewRegistry();
        var pool = NamedConnectionPool<Handle>.For(resources, "test.network", new());
        string name = "sdk-" + Guid.NewGuid().ToString("N");
        using var guard = new CancellationTokenSource(10000);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new Handle(() => { entered.TrySetResult(); if (!release.Wait(10000)) throw new TimeoutException("test release barrier"); });
        await pool.OpenAsync(name, _ => Task.FromResult(old), 1000, guard.Token);
        var elapsed = Stopwatch.StartNew();
        Cleanup(resources, "CleanupExecution");
        Assert.True(elapsed.ElapsedMilliseconds < 1000, "SDK cleanup must not synchronously wait on the plugin's native release");
        await entered.Task.WaitAsync(guard.Token);
        var nextResources = newDebugRegistry ? NewRegistry() : resources;
        var next = NamedConnectionPool<Handle>.For(nextResources, "test.network", new());
        var current = new Handle(() => { });
        int factoryCalls = 0;
        var reopening = next.OpenAsync(name, _ => { Interlocked.Increment(ref factoryCalls); return Task.FromResult(current); }, 2000, guard.Token);
        try
        {
            Assert.False(reopening.IsCompleted);
            Assert.Equal(0, factoryCalls);
            release.Set();
            Assert.Same(current, await reopening.WaitAsync(guard.Token));
            Assert.Equal(1, old.Releases);
            Assert.Equal(1, factoryCalls);
            Assert.Same(current, next.Get(name));
        }
        finally
        {
            release.Set();
            await reopening.WaitAsync(guard.Token);
            await next.CloseAsync(name, 1000, guard.Token);
            Cleanup(nextResources, "CleanupAll");
        }
    }

    [Fact]
    public async Task Http_client_can_be_recreated_in_the_same_actual_sdk_registry_after_execution_cleanup()
    {
        var resources = NewRegistry();
        var plugin = new HttpClientCreatePlugin();
        var ctx = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)ctx;
        state.Registry = resources;
        state.SetStep(plugin, new HttpClientCreateSetting { BaseUrl = "\"http://example.invalid/\"" });
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
        Assert.True(resources.TryGet<HttpClientResource>("Http.Client.Mes", out var old));
        Cleanup(resources, "CleanupExecution");
        Assert.False(resources.Contains("Http.Client.Mes"));
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
        Assert.True(resources.TryGet<HttpClientResource>("Http.Client.Mes", out var current));
        Assert.NotSame(old, current);
        await old.CloseAsync(1000);
        Assert.Throws<InvalidOperationException>(() => old.CheckPublication(resources, "Http.Client.Mes", default));
        await current.CloseAsync(1000);
        Cleanup(resources, "CleanupAll");
    }

    internal static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(
        typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    internal static void Cleanup(IResourceRegistry registry, string method) => registry.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);

    private sealed class Handle(Action release) : IDisposable
    {
        internal int Releases;
        public void Dispose() { release(); Interlocked.Increment(ref Releases); }
    }
}

public class SdkContext : TestContext
{
    public IResourceRegistry Registry { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Resources"
        ? Registry : base.Invoke(method, args);
}
