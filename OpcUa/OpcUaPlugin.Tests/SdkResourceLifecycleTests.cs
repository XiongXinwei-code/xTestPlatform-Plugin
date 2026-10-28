using System.Diagnostics;
using System.Reflection;
using Opc.Ua.Client;
using OpcUa.Helpers;
using xTestPlatform.Core.Engine;
using Xunit;

namespace OpcUa.Tests;

// 实际 SDK registry + 假关闭/factory；不会连接 OPC UA 服务器，不等同调试窗口 E2E。
public sealed class SdkResourceLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSdkCleanupAllowsFirstSameLogicalReconnectOnlyAfterOldSessionRelease(bool newDebugRegistry)
    {
        var registry = NewRegistry();
        string name = "sdk-" + Guid.NewGuid().ToString("N");
        string key = OpcUaHelper.GetSessionKey(name);
        string retiring = OpcUaHelper.GetRetirementKey("opc.tcp://unit.test:4840", name);
        var releasing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int closed = 0, created = 0;
        var old = new OpcUaConnectionResource(async () =>
        { releasing.TrySetResult(); await release.Task; Interlocked.Increment(ref closed); }, 1000, retirementKey: retiring);
        Assert.True(registry.TryAdd(key, old));
        var elapsed = Stopwatch.StartNew();
        Cleanup(registry, newDebugRegistry ? "CleanupAll" : "CleanupExecution");
        Assert.True(elapsed.ElapsedMilliseconds < 1000, "registry 清理不得同步等待真实 Session 关闭");
        await releasing.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var nextRegistry = newDebugRegistry ? NewRegistry() : registry;
        var next = new OpcUaConnectionResource(2000, retirementKey: retiring);
        Assert.True(nextRegistry.TryAdd(key, next));
        var opening = next.InitializeAsync(_ =>
        {
            Interlocked.Increment(ref created);
            return Task.FromException<Session>(new FactoryReachedException());
        }, 2000, default);
        try
        {
            await Task.WhenAny(opening, Task.Delay(60));
            Assert.False(opening.IsCompleted); Assert.Equal(0, created);
            release.TrySetResult();
            await Assert.ThrowsAsync<FactoryReachedException>(() => opening);
            Assert.Equal(1, closed); Assert.Equal(1, created);
            Assert.True(nextRegistry.TryGet<OpcUaConnectionResource>(key, out var current));
            Assert.Same(next, current);
        }
        finally
        {
            release.TrySetResult(); try { await opening; } catch (FactoryReachedException) { }
            await old.CloseAsync(2000); Cleanup(nextRegistry, "CleanupAll"); await next.CloseAsync(2000);
        }
    }
    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(
        typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry, string name) => registry.GetType().GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
    private sealed class FactoryReachedException : Exception;
}
