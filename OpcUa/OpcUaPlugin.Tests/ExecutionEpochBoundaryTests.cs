using System.Reflection;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Executors;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace OpcUa.Tests;

public sealed class ExecutionEpochBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupInsideNamedLookupCannotCloseNewConnectionOrStopNewCollector(bool collector)
    {
        var registry = NewRegistry(); var proxy = DispatchProxy.Create<IResourceRegistry, EpochForwardingRegistry>();
        var forwarding = (EpochForwardingRegistry)(object)proxy; forwarding.Inner = registry;
        IStepPlugin plugin = collector ? new OpcUaDataAcqStopPlugin() : new OpcUaDisconnectPlugin();
        object setting = collector ? new OpcUaDataAcqStopSetting { TaskName = "\"T\"" } : new OpcUaDisconnectSetting { ConnectionName = "\"C\"" };
        var (context, state) = BoundaryContext.Create(plugin, setting); state.UseRegistry(proxy);
        var freshConnection = new OpcUaConnectionResource(() => Task.CompletedTask, 2000);
        using var freshCollector = new OpcUaDataAcqTask("T", _ => Task.FromResult(new DataValueCollection { new DataValue(1) }), [new() { NodeId = "ns=2;s=V" }], 100, 0, 20);
        string targetKey = collector ? "OpcUaDataAcq_T" : "OpcUa_C";
        forwarding.BeforeNamedLookup = key =>
        {
            if (key != targetKey) return;
            forwarding.BeforeNamedLookup = null; Cleanup(registry, "CleanupExecution");
            var nextEpoch = PluginCommon.ExecutionEpoch.Capture(proxy, "OpcUa"); freshConnection.ExecutionCheck = nextEpoch.ThrowIfExpired;
            if (collector) Assert.True(registry.TryAdd(targetKey, freshCollector)); else Assert.True(registry.TryAdd(targetKey, freshConnection));
        };
        try
        {
            Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.False(freshCollector.IsStopRequested); freshConnection.EnsureAvailableForPublication();
        }
        finally { await freshCollector.StopAsync(2000); await freshConnection.CloseAsync(2000); Cleanup(registry, "CleanupAll"); }
    }

    [Fact]
    public async Task ActualSdkCleanupDuringNativeConnectFailureNeverRemovesNewExecutionOwner()
    {
        var registry = NewRegistry(); var plugin = new OpcUaConnectPlugin();
        var (context, state) = BoundaryContext.Create(plugin, new OpcUaConnectSetting { ConnectionName = "\"C\"", EndpointUrl = "\"opc.tcp://epoch-race:4840\"", TimeoutMs = 2000 }); state.UseRegistry(registry);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nativeExit = new TaskCompletionSource<Session>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fresh = new OpcUaConnectionResource(() => Task.CompletedTask, 2000);
        var execution = new OpcUaConnectExecutor(_ => { entered.TrySetResult(); return nativeExit.Task; }).ExecuteAsync(context);
        OpcUaConnectionResource? old = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Assert.True(registry.TryGet<OpcUaConnectionResource>("OpcUa_C", out var captured)); old = captured;
            Cleanup(registry, "CleanupExecution"); Assert.True(registry.TryAdd("OpcUa_C", fresh));
            nativeExit.TrySetException(new InvalidOperationException("late raw Create failure after SDK cleanup"));
            Assert.Equal(TestStatus.Error, (await execution.WaitAsync(TimeSpan.FromSeconds(10))).StepResult.Status);
            Assert.True(registry.TryGet<OpcUaConnectionResource>("OpcUa_C", out var current)); Assert.Same(fresh, current); Assert.False(fresh.IsClosed);
        }
        finally { nativeExit.TrySetException(new InvalidOperationException("test release")); await execution; if (old != null) await old.CloseAsync(2000); await fresh.CloseAsync(2000); Cleanup(registry, "CleanupAll"); }
    }

    [Fact]
    public async Task HostExplicitAbortIsAbortedWithoutCancellingCallerOrSubmittingSdkRequests()
    {
        IStepPlugin[] plugins = [new OpcUaConnectPlugin(), new OpcUaDisconnectPlugin(), new OpcUaReadPlugin(), new OpcUaWritePlugin(), new OpcUaBatchReadPlugin(), new OpcUaBatchWritePlugin(),
            new OpcUaSubscribePlugin(), new OpcUaDataAcqStartPlugin(), new OpcUaDataAcqReadPlugin(), new OpcUaDataAcqStopPlugin()];
        foreach (var plugin in plugins)
        {
            var registry = NewRegistry(); var (context, state) = BoundaryContext.Create(plugin, plugin.CreateSerializer().CreateDefault()); state.UseRegistry(registry); state.AbortRequested = true;
            state.BeforeCurrentStep = () => throw new InvalidOperationException("host abort must be observed before settings or requests");
            Assert.Equal(TestStatus.Aborted, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Cleanup(registry, "CleanupAll");
        }
    }

    [Fact]
    public async Task AllTenActualEntriesCaptureSdkExecutionBeforeFirstStepAccess()
    {
        IStepPlugin[] plugins = [new OpcUaConnectPlugin(), new OpcUaDisconnectPlugin(), new OpcUaReadPlugin(), new OpcUaWritePlugin(), new OpcUaBatchReadPlugin(), new OpcUaBatchWritePlugin(),
            new OpcUaSubscribePlugin(), new OpcUaDataAcqStartPlugin(), new OpcUaDataAcqReadPlugin(), new OpcUaDataAcqStopPlugin()];
        var missing = new List<string>();
        foreach (var plugin in plugins)
        {
            var registry = NewRegistry(); var (context, state) = BoundaryContext.Create(plugin, plugin.CreateSerializer().CreateDefault()); state.UseRegistry(registry);
            state.BeforeCurrentStep = () => { if (!registry.TryGet<object>("PluginCommon.ExecutionEpoch:OpcUa", out _)) missing.Add(plugin.StepTypeId); throw new InvalidOperationException("pure managed entry probe; never submit hardware"); };
            await plugin.CreateExecutor().ExecuteAsync(context); Cleanup(registry, "CleanupAll");
        }
        Assert.Empty(missing);
    }

    [Fact]
    public async Task OldActualReadCannotLookupNewExecutionConnectionAfterSdkCleanup()
    {
        var registry = NewRegistry(); var (context, state) = BoundaryContext.Create(new OpcUaReadPlugin(), new OpcUaReadSetting
        { ConnectionName = "\"C\"", NodeId = "ns=2;s=V", ResultVariable = "Locals.v" }); state.UseRegistry(registry);
        using var resume = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.BeforeCurrentStep = () => { state.BeforeCurrentStep = null; entered.TrySetResult(); resume.Wait(); };
        var fresh = new OpcUaConnectionResource(() => Task.CompletedTask, 2000); int reads = 0;
        var executor = new OpcUaReadExecutor((_, _) => { Interlocked.Increment(ref reads); return Task.FromResult(new DataValue(1) { StatusCode = StatusCodes.Good }); });
        var execution = Task.Run(() => executor.ExecuteAsync(context));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Cleanup(registry, "CleanupExecution"); Assert.True(registry.TryAdd("OpcUa_C", fresh)); resume.Set();
            Assert.Equal(TestStatus.Error, (await execution.WaitAsync(TimeSpan.FromSeconds(10))).StepResult.Status); Assert.Equal(0, reads); Assert.Empty(state.Published); Assert.False(fresh.IsClosed);
        }
        finally { resume.Set(); await execution; await fresh.CloseAsync(2000); Cleanup(registry, "CleanupAll"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSdkCleanupRejectsUnregisteredConnectWithoutCallerCancellation(bool cleanupAll)
    {
        var registry = NewRegistry(); var plugin = new OpcUaConnectPlugin();
        var (context, state) = BoundaryContext.Create(plugin, new OpcUaConnectSetting { ConnectionName = "\"C\"", EndpointUrl = "\"opc.tcp://unit.test:4840\"" }); state.UseRegistry(registry);
        using var resume = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.BeforeCurrentStep = () => { state.BeforeCurrentStep = null; entered.TrySetResult(); resume.Wait(); };
        int created = 0; var executor = new OpcUaConnectExecutor(_ => { Interlocked.Increment(ref created); return Task.FromResult<Session>(null!); });
        var execution = Task.Run(() => executor.ExecuteAsync(context));
        IResourceRegistry next = registry;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Cleanup(registry, cleanupAll ? "CleanupAll" : "CleanupExecution"); resume.Set();
            var result = await execution.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, created);
            Assert.False(registry.TryGet<OpcUaConnectionResource>("OpcUa_C", out _));
            next = cleanupAll ? NewRegistry() : registry; state.UseRegistry(next);
            Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(context)).StepResult.Status); Assert.Equal(1, created);
        }
        finally
        {
            resume.Set(); await execution;
            if (registry.TryGet<OpcUaConnectionResource>("OpcUa_C", out var old)) await old.CloseAsync(2000);
            if (next.TryGet<OpcUaConnectionResource>("OpcUa_C", out var current)) await current.CloseAsync(2000);
            Cleanup(registry, "CleanupAll"); if (!ReferenceEquals(next, registry)) Cleanup(next, "CleanupAll");
        }
    }

    [Fact]
    public async Task ActualSdkCleanupDoesNotLetOldStartAttachToNewExecutionConnection()
    {
        var registry = NewRegistry(); var plugin = new OpcUaDataAcqStartPlugin();
        var setting = new OpcUaDataAcqStartSetting { TaskName = "\"T\"", ConnectionName = "\"C\"", Items = [new() { NodeId = "ns=2;s=V" }] };
        var (context, state) = BoundaryContext.Create(plugin, setting); state.UseRegistry(registry);
        using var resume = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.BeforeCurrentStep = () => { state.BeforeCurrentStep = null; entered.TrySetResult(); resume.Wait(); };
        var execution = Task.Run(() => plugin.CreateExecutor().ExecuteAsync(context));
        var fresh = new OpcUaConnectionResource(() => Task.CompletedTask, 2000)
        { AcquisitionReader = (_, _) => Task.FromResult(new DataValueCollection { new DataValue(1) { StatusCode = StatusCodes.Good } }) };
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Cleanup(registry, "CleanupExecution"); Assert.True(registry.TryAdd("OpcUa_C", fresh)); resume.Set();
            var result = await execution.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.False(registry.TryGet<OpcUaDataAcqTask>("OpcUaDataAcq_T", out _)); Assert.False(fresh.IsClosed);
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
        }
        finally { resume.Set(); await execution; await fresh.CloseAsync(2000); Cleanup(registry, "CleanupAll"); }
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
