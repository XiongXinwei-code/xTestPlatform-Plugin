using System.Reflection;
using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class SdkRestartLifecycleTests
{
    [Fact]
    public async Task CleanupWhileWaitingForAxisGatePreventsBindingIntoTheNextExecution()
    {
        using var guard = new CancellationTokenSource(10000);
        var registry = NewRegistry(); var plugin = new BindAxisPlugin(); var config = new BindAxisSetting();
        var context = Context(registry, plugin, config);
        var enter = typeof(AxisSession).Assembly.GetType("CiA402.Core.AxisBindingGate", true)!.GetMethod("EnterAsync", BindingFlags.Public | BindingFlags.Static)!;
        var gate = await (Task<IDisposable>)enter.Invoke(null, [context, AxisSession.Key("Axis1"), guard.Token])!;
        var binding = plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
        Assert.False(binding.IsCompleted);
        Cleanup(registry, "CleanupExecution");
        // New execution already established its parent; the old bind must not consume it.
        using var newDriver = new FakeCanAdapter(); newDriver.Object(0x6040, 0, 0, 2); newDriver.Object(0x6041, 0, 0x40, 2);
        registry.Set(CanHelper.GetAdapterKey("CAN1"), newDriver);
        gate.Dispose();
        try
        {
            Assert.Equal(TestStatus.Error, (await binding.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Empty(newDriver.Sent);
            Assert.False(registry.Contains(AxisSession.Key("Axis1")));
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(Context(registry, plugin, config), guard.Token)).StepResult.Status);
        }
        finally
        {
            if (registry.TryGet<AxisSession>(AxisSession.Key("Axis1"), out var axis)) await axis.CloseAsync();
            Cleanup(registry, "CleanupAll");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SdkCleanupOfAxisAndParentAllowsFirstBindOnNewBusGeneration(bool newDebugRegistry)
    {
        using var guard = new CancellationTokenSource(15000);
        var registry = NewRegistry(); string physicalKey = "cia-sdk-" + Guid.NewGuid().ToString("N");
        var oldDriver = new GatedCan { BlockClose = true }; var oldOwner = new CanConnectionResource(oldDriver, physicalKey);
        registry.Set(CanHelper.GetAdapterKey("CAN1"), oldOwner);
        var plugin = new BindAxisPlugin(); var setting = new BindAxisSetting();
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(Context(registry, plugin, setting), guard.Token)).StepResult.Status);
        Assert.True(registry.TryGet<AxisSession>(AxisSession.Key("Axis1"), out var oldAxis));
        Cleanup(registry, newDebugRegistry ? "CleanupAll" : "CleanupExecution");
        var nextRegistry = newDebugRegistry ? NewRegistry() : registry;
        await oldDriver.CloseEntered.Task.WaitAsync(guard.Token);
        var nextDriver = new GatedCan(); CanConnectionResource? nextOwner = null;
        async Task<ExecutionResult> Rebind()
        {
            await CanConnectionResource.WaitForPreviousOwnerAsync(physicalKey, 5000, guard.Token);
            nextOwner = new CanConnectionResource(nextDriver, physicalKey);
            nextRegistry.Set(CanHelper.GetAdapterKey("CAN1"), nextOwner);
            return await plugin.CreateExecutor().ExecuteAsync(Context(nextRegistry, plugin, setting), guard.Token);
        }
        var rebind = Rebind();
        try
        {
            Assert.False(rebind.IsCompleted); Assert.Null(nextOwner); Assert.Empty(nextDriver.Raw.Sent);
            oldDriver.Release.Set();
            Assert.Equal(TestStatus.Completed, (await rebind.WaitAsync(guard.Token)).StepResult.Status);
            await oldAxis.Released.WaitAsync(guard.Token);
            Assert.True(nextRegistry.TryGet<AxisSession>(AxisSession.Key("Axis1"), out var current));
            Assert.NotSame(oldAxis, current); Assert.True(nextOwner!.IsConnected);
            // Repeated old-axis disposal cannot release the replacement parent's lease or channel.
            oldAxis.Dispose();
            Assert.True(nextOwner.IsConnected); Assert.Equal(0, nextDriver.CloseCount);
            Assert.All(nextDriver.Raw.Sent, frame => Assert.Equal(0x40, frame.Data[0]));
            await current.CloseAsync();
            Assert.True(nextOwner.IsConnected); Assert.Equal(0, nextDriver.CloseCount);
        }
        finally
        {
            oldDriver.Release.Set(); await rebind.WaitAsync(guard.Token);
            Cleanup(nextRegistry, "CleanupAll");
            if (nextOwner != null) await nextOwner.CloseAsync(2000, guard.Token);
            await oldOwner.CloseAsync(2000, guard.Token);
        }
    }

    private static IExecutionContext Context(IResourceRegistry registry, BindAxisPlugin plugin, BindAxisSetting setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkAxisContext>();
        var state = (SdkAxisContext)(object)context; state.Registry = registry;
        var original = TestContext.Create(plugin, setting); state.Current = original.State.Current;
        return context;
    }
    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry, string method) => registry.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);

    private sealed class GatedCan : ICanAdapter
    {
        internal readonly FakeCanAdapter Raw = new();
        internal readonly ManualResetEventSlim Release = new();
        internal readonly TaskCompletionSource CloseEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BlockClose; internal int CloseCount;
        internal GatedCan() { Raw.Object(0x6040, 0, 0, 2); Raw.Object(0x6041, 0, 0x40, 2); }
        public bool IsConnected => Raw.IsConnected;
        public void Open(CanAdapterConfig config) => Raw.Open(config);
        public void Write(CanMessage message) => Raw.Write(message);
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => Raw.Read(timeoutMs, ct);
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Raw.Read(id, timeoutMs, ct);
        public void Close() { CloseEntered.TrySetResult(); if (BlockClose && !Release.Wait(15000)) throw new TimeoutException("test close barrier"); CloseCount++; Raw.Close(); }
        public void Dispose() { }
    }
}

public class SdkAxisContext : TestContext
{
    public IResourceRegistry Registry { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Resources" ? Registry : base.Invoke(method, args);
}
