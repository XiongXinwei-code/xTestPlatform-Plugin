using System.Diagnostics;
using System.Reflection;
using NiDaq.Helpers;
using xTestPlatform.Core.Engine;
using Xunit;

namespace NiDaq.ContractTests;

// 实际 SDK registry + 假原生释放；不创建 DAQmx Task，不等同完整调试窗口/设备 E2E。
public sealed class SdkResourceLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSdkCleanupAllowsFirstReconfigureOnlyAfterOldPhysicalOwnerExits(bool newDebugRegistry)
    {
        var registry = NewRegistry();
        var context = RegistryContext.Create(registry);
        string physical = "Fake" + Guid.NewGuid().ToString("N") + "/ai0";
        using var release = new ManualResetEventSlim();
        var releasing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int oldReleases = 0, configurations = 0;
        var old = new DaqTaskResource(() => { }, () =>
        {
            releasing.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("test release gate");
            Interlocked.Increment(ref oldReleases);
        }, [physical]);
        await DaqTaskFactory.CreateAndRegisterAsync(context, "T", _ => { }, default, old);
        var elapsed = Stopwatch.StartNew();
        Cleanup(registry, newDebugRegistry ? "CleanupAll" : "CleanupExecution");
        Assert.True(elapsed.ElapsedMilliseconds < 1000, "registry 清理不得同步等待原生释放");
        await releasing.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var nextRegistry = newDebugRegistry ? NewRegistry() : registry;
        var nextContext = RegistryContext.Create(nextRegistry);
        var next = new DaqTaskResource(() => { }, () => { }, [physical]);
        var reopening = DaqTaskFactory.CreateAndRegisterAsync(nextContext, "T", _ => Interlocked.Increment(ref configurations), default, next, timeoutMs: 2000);
        try
        {
            await Task.WhenAny(reopening, Task.Delay(60));
            Assert.False(reopening.IsCompleted); Assert.Equal(0, configurations);
            release.Set(); await reopening.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1, oldReleases); Assert.Equal(1, configurations);
            Assert.Same(next, NiDaqTaskRegistry.Get(nextContext, "T"));
        }
        finally
        {
            release.Set(); await reopening; await old.CloseAsync(2000, default);
            Cleanup(nextRegistry, "CleanupAll"); await next.CloseAsync(2000, default);
        }
    }

    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(
        typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry, string name) => registry.GetType().GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
}

public class RegistryContext : DispatchProxy
{
    private IResourceRegistry _resources = null!;
    public static IExecutionContext Create(IResourceRegistry registry)
    {
        var context = DispatchProxy.Create<IExecutionContext, RegistryContext>();
        ((RegistryContext)(object)context)._resources = registry; return context;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Resources"
        ? _resources : method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
}
