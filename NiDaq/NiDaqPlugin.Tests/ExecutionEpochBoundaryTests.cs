using System.Reflection;
using AuditTestSupport;
using NiDaq.Executors;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ExecutionEpochBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupInsideNamedLookupCannotStartOrCloseNewExecutionOwner(bool stop)
    {
        var registry = NewRegistry(); var proxy = DispatchProxy.Create<IResourceRegistry, EpochForwardingRegistry>();
        var forwarding = (EpochForwardingRegistry)(object)proxy; forwarding.Inner = registry;
        IStepPlugin plugin = stop ? new NiDaqTaskStopPlugin() : new NiDaqTaskStartPlugin();
        object setting = stop ? new NiDaqTaskStopSetting { TaskName = "\"T\"" } : new NiDaqTaskStartSetting { TaskName = "\"T\"" };
        var (context, state) = ContextProxy.Create(plugin, setting); state.Store = proxy;
        int starts = 0; var fresh = new DaqTaskResource(() => { }, () => { });
        var plan = AoOutputPlan.Create(new NiDaqAoConfigSetting { Channels = [new() { PhysicalChannel = "FakeLookup/ao0" }], StopPolicy = AoStopPolicy.DeviceDefined }, [1]);
        await fresh.ConfigureAsync(() => fresh.ConfigureAnalogOutput(plan, _ => { }, () => Interlocked.Increment(ref starts), () => { }), default);
        forwarding.BeforeNamedLookup = key =>
        {
            if (key != "NiDaq.Task:T") return;
            forwarding.BeforeNamedLookup = null; Cleanup(registry, "CleanupExecution");
            var nextEpoch = PluginCommon.ExecutionEpoch.Capture(proxy, "NiDaq"); fresh.ExecutionCheck = nextEpoch.ThrowIfExpired;
            Assert.True(registry.TryAdd(key, fresh));
        };
        try
        {
            Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.False(fresh.IsClosing || fresh.IsClosed || fresh.Started); Assert.Equal(0, starts);
        }
        finally { await fresh.CloseAsync(2000, default); Cleanup(registry, "CleanupAll"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSdkCleanupTracksPendingTemporaryDigitalOwnerUntilNextExecutorCanConfigure(bool output)
    {
        string channel = "FakePending" + Guid.NewGuid().ToString("N") + "/port0/line0";
        IStepPlugin plugin = output ? new NiDaqDoWritePlugin() : new NiDaqDiReadPlugin();
        object setting = output ? new NiDaqDoWriteSetting { Channel = "\"" + channel + "\"", Value = "\"1\"", TimeoutMs = 4000 }
            : new NiDaqDiReadSetting { Channel = "\"" + channel + "\"", ResultVariable = "Locals.v", TimeoutMs = 4000 };
        var registry = NewRegistry(); var (context, state) = ContextProxy.Create(plugin, setting); state.Store = registry;
        using var nativeExit = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int configured = 0; DaqTaskResource? firstOwner = null;
        IStepExecutor executor = output ? new NiDaqDoWriteExecutor((owner, _) => { firstOwner ??= owner; Interlocked.Increment(ref configured); }, (_, _, _, _) =>
            { if (Volatile.Read(ref configured) == 1) { entered.TrySetResult(); nativeExit.Wait(); } })
            : new NiDaqDiReadExecutor((owner, _) => { firstOwner ??= owner; Interlocked.Increment(ref configured); }, _ =>
            { if (Volatile.Read(ref configured) == 1) { entered.TrySetResult(); nativeExit.Wait(); } return 1; });
        var old = executor.ExecuteAsync(context); Task<ExecutionResult>? next = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Cleanup(registry, "CleanupExecution");
            next = executor.ExecuteAsync(context);
            await Task.Delay(80); Assert.Equal(1, Volatile.Read(ref configured)); Assert.False(next.IsCompleted);
            nativeExit.Set(); Assert.Equal(TestStatus.Error, (await old.WaitAsync(TimeSpan.FromSeconds(8))).StepResult.Status);
            Assert.Equal(TestStatus.Completed, (await next.WaitAsync(TimeSpan.FromSeconds(8))).StepResult.Status);
            Assert.True(firstOwner!.IsClosed); Assert.Equal(2, configured);
        }
        finally { nativeExit.Set(); await old; if (next != null) await next; if (firstOwner != null) await firstOwner.CloseAsync(3000, default); Cleanup(registry, "CleanupAll"); }
    }

    [Fact]
    public async Task ActualSdkCleanupRetiresEpochButAoFinalCleanupStillWaitsForActualWriterAndRunsOnce()
    {
        var registry = NewRegistry(); var (context, state) = ContextProxy.Create(new NiDaqAoConfigPlugin(), new NiDaqAoConfigSetting()); state.Store = registry;
        var guard = new DaqExecutionGuard(context, default);
        var calls = new List<string>(); using var nativeExit = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool finalSawRetiredEpoch = false;
        var owner = new DaqTaskResource(() => throw new InvalidOperationException("AO must not Abort concurrently"), () => { lock (calls) calls.Add("release"); });
        var plan = AoOutputPlan.Create(new NiDaqAoConfigSetting
        {
            Channels = [new() { PhysicalChannel = "FakeEpoch/ao0", MinValue = -5, MaxValue = 5 }],
            StopPolicy = AoStopPolicy.WriteFinalValues, CleanupTimeoutMs = 2000
        }, [1], [0.5]);
        await DaqTaskFactory.CreateAndRegisterAsync(context, "AO", resource => resource.ConfigureAnalogOutput(plan, values =>
        {
            if (values[0] == 2) { entered.TrySetResult(); nativeExit.Wait(); }
            if (values[0] == 0.5)
            {
                try { guard.Epoch.ThrowIfExpired(); }
                catch (InvalidOperationException) { finalSawRetiredEpoch = true; }
            }
            lock (calls) calls.Add("write:" + values[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
        }, () => { lock (calls) calls.Add("start"); }, () => { lock (calls) calls.Add("stop"); }), default, owner, epoch: guard.Epoch);
        await owner.StartAsync(default);
        var writing = owner.WriteAnalogAsync([2], 2000, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Cleanup(registry, "CleanupExecution");
            Assert.Throws<InvalidOperationException>(() => guard.Epoch.ThrowIfExpired());
            lock (calls) Assert.DoesNotContain("release", calls);
            nativeExit.Set(); await Assert.ThrowsAnyAsync<Exception>(() => writing);
            await owner.CloseAsync(2000, default);
            Assert.True(finalSawRetiredEpoch);
            Assert.Equal(["start", "write:1", "write:2", "write:0.5", "stop", "release"], calls);
            await owner.CloseAsync(2000, default); Assert.Equal(1, calls.Count(c => c == "write:0.5"));
        }
        finally { nativeExit.Set(); try { await writing; } catch { } await owner.CloseAsync(2000, default); Cleanup(registry, "CleanupAll"); }
    }

    [Fact]
    public async Task HostExplicitAbortIsAbortedWithoutCancellingCallerOrSubmittingHardware()
    {
        IStepPlugin[] plugins = [new NiDaqAiConfigPlugin(), new NiDaqAiReadPlugin(), new NiDaqAoConfigPlugin(), new NiDaqAoWriteSinglePlugin(), new NiDaqEncoderConfigPlugin(), new NiDaqEncoderReadPlugin(),
            new NiDaqSyncConfigPlugin(), new NiDaqSyncReadPlugin(), new NiDaqDiReadPlugin(), new NiDaqDoWritePlugin(), new NiDaqTaskStartPlugin(), new NiDaqTaskStopPlugin()];
        foreach (var plugin in plugins)
        {
            var registry = NewRegistry(); var (context, state) = ContextProxy.Create(plugin, plugin.CreateSerializer().CreateDefault()); state.Store = registry; state.AbortRequested = true;
            state.BeforeCurrentStep = () => throw new InvalidOperationException("host abort must be observed before settings or hardware");
            Assert.Equal(TestStatus.Aborted, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Cleanup(registry, "CleanupAll");
        }
    }

    [Fact]
    public async Task AllTwelveActualEntriesCaptureSdkExecutionBeforeFirstStepAccess()
    {
        IStepPlugin[] plugins = [new NiDaqAiConfigPlugin(), new NiDaqAiReadPlugin(), new NiDaqAoConfigPlugin(), new NiDaqAoWriteSinglePlugin(), new NiDaqEncoderConfigPlugin(), new NiDaqEncoderReadPlugin(),
            new NiDaqSyncConfigPlugin(), new NiDaqSyncReadPlugin(), new NiDaqDiReadPlugin(), new NiDaqDoWritePlugin(), new NiDaqTaskStartPlugin(), new NiDaqTaskStopPlugin()];
        var missing = new List<string>();
        foreach (var plugin in plugins)
        {
            var registry = NewRegistry(); var (context, state) = ContextProxy.Create(plugin, plugin.CreateSerializer().CreateDefault()); state.Store = registry;
            state.BeforeCurrentStep = () => { if (!registry.TryGet<object>("PluginCommon.ExecutionEpoch:NiDaq", out _)) missing.Add(plugin.StepTypeId); throw new InvalidOperationException("pure managed entry probe; never submit hardware"); };
            await plugin.CreateExecutor().ExecuteAsync(context); Cleanup(registry, "CleanupAll");
        }
        Assert.Empty(missing);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSdkCleanupPreventsLateDigitalIoCreation(bool output)
    {
        IStepPlugin plugin = output ? new NiDaqDoWritePlugin() : new NiDaqDiReadPlugin();
        object setting = output ? new NiDaqDoWriteSetting { Channel = "\"FakeEpoch/port0\"", Value = "\"1\"" }
            : new NiDaqDiReadSetting { Channel = "\"FakeEpoch/port0\"", ResultVariable = "Locals.v" };
        var registry = NewRegistry(); var (context, state) = ContextProxy.Create(plugin, setting); state.Store = registry;
        using var resume = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.BeforeCurrentStep = () => { state.BeforeCurrentStep = null; entered.TrySetResult(); resume.Wait(); };
        int io = 0;
        IStepExecutor executor = output ? new NiDaqDoWriteExecutor((_, _) => { }, (_, _, _, _) => Interlocked.Increment(ref io))
            : new NiDaqDiReadExecutor((_, _) => { }, _ => { Interlocked.Increment(ref io); return 1u; });
        var execution = Task.Run(() => executor.ExecuteAsync(context));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Cleanup(registry, "CleanupExecution"); resume.Set();
            Assert.Equal(TestStatus.Error, (await execution.WaitAsync(TimeSpan.FromSeconds(10))).StepResult.Status); Assert.Equal(0, io); Assert.Empty(state.Published);
        }
        finally { resume.Set(); await execution; Cleanup(registry, "CleanupAll"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSdkCleanupRejectsUnregisteredConfigEvenWithoutCallerCancellation(bool cleanupAll)
    {
        var registry = NewRegistry();
        var setting = new NiDaqSyncConfigSetting { TaskName = "\"T\"", AiChannels = [new() { PhysicalChannel = "FakeEpoch/ai0" }], EncoderChannels = [new() { CounterChannel = "FakeEpoch/ctr0" }] };
        var (context, state) = ContextProxy.Create(new NiDaqSyncConfigPlugin(), setting); state.Store = registry;
        using var resume = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.BeforeCurrentStep = () => { state.BeforeCurrentStep = null; entered.TrySetResult(); resume.Wait(); };
        int configured = 0; var owners = new List<DaqTaskResource>();
        var executor = new NiDaqSyncConfigExecutor((_, _) => Interlocked.Increment(ref configured), channels =>
        { var owner = new DaqTaskResource(() => { }, () => { }, channels); owners.Add(owner); return owner; });
        var execution = Task.Run(() => executor.ExecuteAsync(context));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Cleanup(registry, cleanupAll ? "CleanupAll" : "CleanupExecution"); resume.Set();
            var result = await execution.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, configured);
            Assert.False(registry.TryGet<DaqTaskResource>("NiDaq.Task:T", out _));
            state.Store = cleanupAll ? NewRegistry() : registry;
            Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(context)).StepResult.Status); Assert.Equal(1, configured);
        }
        finally { resume.Set(); await execution; foreach (var owner in owners) await owner.CloseAsync(2000, default); Cleanup((IResourceRegistry)state.Store!, "CleanupAll"); }
    }
    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry, string name) => registry.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
}

public class EpochForwardingRegistry : DispatchProxy
{
    public IResourceRegistry Inner = null!;
    public Action<string>? BeforeNamedLookup;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet") BeforeNamedLookup?.Invoke((string)args![0]!);
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
