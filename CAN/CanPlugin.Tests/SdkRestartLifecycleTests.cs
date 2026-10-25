using System.Reflection;
using CAN.Adapters;
using CAN.Executors;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class SdkRestartLifecycleTests
{
    [Fact]
    public async Task OldCloseCannotFreezeOwnerReturnedFromNewSdkExecution()
    {
        var actual = NewRegistry(); var forwarding = DispatchProxy.Create<IResourceRegistry, LookupCanRegistry>(); var proxy = (LookupCanRegistry)(object)forwarding; proxy.Inner = actual;
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>(); var state = (SdkCanContext)(object)context; state.Registry = forwarding;
        state.Plugin = new CanClosePlugin(); state.Setting = new CanCloseSetting();
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        proxy.BeforeLookup = () => { Cleanup(actual, "CleanupExecution"); actual.Set(CanHelper.GetAdapterKey("CAN1"), owner); };
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.False(owner.IsClosing); Assert.Equal(0, raw.CloseCount);
        }
        finally { await owner.CloseAsync(); }
    }

    private static int _channel = 300000;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SdkCleanupBeforeRegistrationPreventsLateOpenWithoutCallerCancellation(bool physicalWait)
    {
        using var guard = new CancellationTokenSource(10000);
        var registry = NewRegistry();
        string channel = Interlocked.Increment(ref _channel).ToString();
        var setting = new CanOpenSetting { AdapterType = CanAdapterType.Kvaser, Channel = $"\"{channel}\"", ConnectionName = "\"CAN1\"", TimeoutMs = 5000 };
        IDisposable? lease = null; CanConnectionResource? retiring = null;
        var old = new GatedDriver { BlockClose = true };
        if (physicalWait)
        {
            retiring = new CanConnectionResource(old, CanChannelIdentity.Normalize(CanAdapterType.Kvaser, channel));
            retiring.Dispose(); await old.CloseEntered.Task.WaitAsync(guard.Token);
        }
        else lease = await CanRegistryGuard.EnterAsync(registry, "CAN1", guard.Token);
        int factories = 0;
        var executor = new CanOpenExecutor(_ => { Interlocked.Increment(ref factories); return new GatedDriver(); });
        var obsolete = executor.ExecuteAsync(Context(registry, setting), guard.Token);
        Assert.False(obsolete.IsCompleted);
        Cleanup(registry, "CleanupExecution");
        old.Release.Set(); lease?.Dispose();
        try
        {
            var result = await obsolete.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, factories);
            Assert.False(registry.Contains(CanHelper.GetAdapterKey("CAN1")));
            Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(Context(registry, setting), guard.Token)).StepResult.Status);
            Assert.Equal(1, factories);
        }
        finally
        {
            old.Release.Set();
            if (retiring != null) await retiring.CloseAsync(2000, guard.Token);
            if (registry.TryGet<ICanAdapter>(CanHelper.GetAdapterKey("CAN1"), out var current)) await ((CanConnectionResource)current).CloseAsync(2000, guard.Token);
            Cleanup(registry, "CleanupAll");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledNativeOpenAndSdkCleanupKeepFirstReopenBehindActualOldExit(bool newDebugRegistry)
    {
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        var registry = NewRegistry();
        var setting = new CanOpenSetting { AdapterType = CanAdapterType.Kvaser, Channel = $"\"{Interlocked.Increment(ref _channel)}\"", ConnectionName = "\"CAN1\"", TimeoutMs = 5000 };
        var firstContext = Context(registry, setting);
        var old = new GatedDriver { BlockOpen = true };
        var opening = new CanOpenExecutor(_ => old).ExecuteAsync(firstContext, caller.Token);
        await old.Entered.Task.WaitAsync(guard.Token);
        caller.Cancel();
        Assert.Equal(TestStatus.Aborted, (await opening.WaitAsync(guard.Token)).StepResult.Status);
        Cleanup(registry, newDebugRegistry ? "CleanupAll" : "CleanupExecution");
        var nextRegistry = newDebugRegistry ? NewRegistry() : registry;
        var next = new GatedDriver(); int created = 0;
        var reopening = new CanOpenExecutor(_ => { Interlocked.Increment(ref created); return next; }).ExecuteAsync(Context(nextRegistry, setting), guard.Token);
        try
        {
            Assert.False(reopening.IsCompleted); Assert.Equal(0, created); Assert.Equal(0, old.CloseCount);
            old.Release.Set();
            Assert.Equal(TestStatus.Completed, (await reopening.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(1, created); Assert.Equal(1, old.CloseCount);
            Assert.True(nextRegistry.TryGet<ICanAdapter>(CanHelper.GetAdapterKey("CAN1"), out var current));
            Assert.True(current.IsConnected);
            // Late/repeated old cleanup must not remove the new physical-generation claim.
            if (newDebugRegistry) Cleanup(registry, "CleanupAll");
            Assert.True(current.IsConnected); Assert.Equal(0, next.CloseCount);
            await ((CanConnectionResource)current).CloseAsync(2000, guard.Token);
        }
        finally
        {
            old.Release.Set(); await reopening.WaitAsync(guard.Token);
            Cleanup(nextRegistry, "CleanupAll");
        }
    }

    [Fact]
    public async Task SuccessfulOpenLogCannotExtendConfiguredTotalDeadline()
    {
        var registry = NewRegistry();
        var setting = new CanOpenSetting { AdapterType = CanAdapterType.Kvaser, Channel = $"\"{Interlocked.Increment(ref _channel)}\"", ConnectionName = "\"CAN1\"", TimeoutMs = 1000 };
        var context = Context(registry, setting);
        ((SdkCanContext)(object)context).OnLog = text => { if (text.Contains("CAN 通道已打开")) Thread.Sleep(1100); };
        try
        {
            var result = await new CanOpenExecutor(_ => new GatedDriver()).ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("总期限", result.StepResult.Error!.Message);
            if (registry.TryGet<ICanAdapter>(CanHelper.GetAdapterKey("CAN1"), out var owner)) Assert.True(((CanConnectionResource)owner).IsClosing);
        }
        finally
        {
            if (registry.TryGet<ICanAdapter>(CanHelper.GetAdapterKey("CAN1"), out var current)) await ((CanConnectionResource)current).CloseAsync();
            Cleanup(registry, "CleanupAll");
        }
    }

    [Fact]
    public async Task CancellationInSuccessfulOpenLogDoesNotReturnCompleted()
    {
        var registry = NewRegistry(); using var caller = new CancellationTokenSource();
        var setting = new CanOpenSetting { AdapterType = CanAdapterType.Kvaser, Channel = $"\"{Interlocked.Increment(ref _channel)}\"", ConnectionName = "\"CAN1\"" };
        var context = Context(registry, setting);
        ((SdkCanContext)(object)context).OnLog = text => { if (text.Contains("CAN 通道已打开")) caller.Cancel(); };
        try
        {
            var result = await new CanOpenExecutor(_ => new GatedDriver()).ExecuteAsync(context, caller.Token);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            if (registry.TryGet<ICanAdapter>(CanHelper.GetAdapterKey("CAN1"), out var cancelledOwner)) Assert.False(cancelledOwner.IsConnected);
        }
        finally
        {
            if (registry.TryGet<ICanAdapter>(CanHelper.GetAdapterKey("CAN1"), out var current)) await ((CanConnectionResource)current).CloseAsync();
            Cleanup(registry, "CleanupAll");
        }
    }

    private static IExecutionContext Context(IResourceRegistry registry, CanOpenSetting setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        var state = (SdkCanContext)(object)context;
        state.Registry = registry; state.Plugin = new CanOpenPlugin(); state.Setting = setting;
        return context;
    }
    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry, string method) => registry.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);

    private sealed class GatedDriver : ICanAdapter
    {
        internal bool BlockOpen, BlockClose; internal int CloseCount;
        internal readonly ManualResetEventSlim Release = new();
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CloseEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsConnected { get; private set; }
        public void Open(CanAdapterConfig config) { Entered.TrySetResult(); if (BlockOpen && !Release.Wait(10000)) throw new TimeoutException("test open barrier"); IsConnected = true; }
        public void Close() { CloseEntered.TrySetResult(); if (BlockClose && !Release.Wait(10000)) throw new TimeoutException("test close barrier"); CloseCount++; IsConnected = false; }
        public void Dispose() { }
        public void Write(CanMessage message) { }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
    }
}

public class SdkCanContext : CloseContext
{
    public IResourceRegistry Registry { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Resources" ? Registry : base.Invoke(method, args);
}

public class LookupCanRegistry : DispatchProxy
{
    public IResourceRegistry Inner = null!; public Action? BeforeLookup;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet" && Equals(args?[0], CanHelper.GetAdapterKey("CAN1"))) { var action = BeforeLookup; BeforeLookup = null; action?.Invoke(); }
        return method.Invoke(Inner, args);
    }
}
