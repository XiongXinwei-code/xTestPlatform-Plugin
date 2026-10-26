using System.Reflection;
using AuditTestSupport;
using NiDaq.Executors;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class OwnerStopMechanismTests
{
    [Theory]
    [InlineData("CleanupExecution")]
    [InlineData("CleanupAll")]
    public async Task SdkCleanupEndsActiveAndQueuedStepsBeforeRealNativeReadExits(string cleanup)
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var (context, state) = ContextProxy.Create(new NiDaqAiReadPlugin(), new NiDaqAiReadSetting
        { TaskName = "\"T\"", ReadTimeoutMs = -1, ResultVariable = "Locals.Wave" });
        state.Store = registry;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var nativeExit = new ManualResetEventSlim();
        int releases = 0, reads = 0;
        var owner = new DaqTaskResource(() => { }, () => Interlocked.Increment(ref releases));
        await DaqTaskFactory.CreateAndRegisterAsync(context, "T", _ => { }, default, owner);
        var executor = new NiDaqAiReadExecutor((_, _, _, _) =>
        {
            Interlocked.Increment(ref reads); entered.TrySetResult(); nativeExit.Wait();
            return new(new double[,] { { 1 } }, ["AI"], 1);
        });
        var first = executor.ExecuteAsync(context); Task? queued = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var second = executor.ExecuteAsync(context); queued = second;
            registry.GetType().GetMethod(cleanup, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
            Assert.Equal(TestStatus.Error, (await first.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.Equal(TestStatus.Error, (await second.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.False(owner.IsClosed); Assert.Equal(0, releases); Assert.Equal(1, reads); Assert.Empty(state.Published);
            nativeExit.Set(); await owner.CloseAsync(3000, default);
            Assert.True(owner.IsClosed); Assert.Equal(1, releases);
        }
        finally
        {
            nativeExit.Set(); await first.WaitAsync(TimeSpan.FromSeconds(3));
            if (queued != null) await queued.WaitAsync(TimeSpan.FromSeconds(3));
            await owner.CloseAsync(3000, default);
            registry.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
        }
    }
}
