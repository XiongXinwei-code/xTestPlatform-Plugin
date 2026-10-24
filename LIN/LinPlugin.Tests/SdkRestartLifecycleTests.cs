using System.Reflection;
using LIN.Adapters;
using LIN.Executors;
using LIN.Helpers;
using LIN.Models;
using LIN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace LIN.Tests;

public sealed class SdkRestartLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OldCloseOrStopCannotFreezeResourceFromLookupOfNewExecution(bool cyclic)
    {
        var actual = NewRegistry(); var forwarding = DispatchProxy.Create<IResourceRegistry, LookupLinRegistry>(); var stateRegistry = (LookupLinRegistry)(object)forwarding; stateRegistry.Inner = actual;
        var context = DispatchProxy.Create<IExecutionContext, SdkLinContext>(); var state = (SdkLinContext)(object)context; state.Registry = forwarding;
        state.Plugin = cyclic ? new LinCyclicSendStopPlugin() : new LinClosePlugin();
        state.Setting = cyclic ? new LinCyclicSendStopSetting { TaskName = "\"job\"" } : new LinCloseSetting { ConnectionName = "\"LIN1\"" };
        var raw = new GatedDriver(); var owner = new LinConnectionResource(raw); owner.Open(new() { Channel = "lookup-" + Guid.NewGuid().ToString("N") });
        var key = cyclic ? LinCyclicSendStartExecutor.GetTaskKey("job") : LinHelper.GetAdapterKey("LIN1");
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = owner.StartJob([async token => { try { await Task.Delay(Timeout.Infinite, token); } finally { cancelled.TrySetResult(); } }]);
        stateRegistry.Key = key;
        stateRegistry.BeforeLookup = () => { Cleanup(actual, "CleanupExecution"); if (cyclic) actual.Set(key, job); else actual.Set(key, owner); };
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.True(owner.IsConnected); Assert.False(cancelled.Task.IsCompleted); Assert.Equal(0, raw.CloseCount);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SdkCleanupBeforeRegistrationPreventsLateOpenWithoutCallerCancellation(bool physicalWait)
    {
        using var guard = new CancellationTokenSource(10000);
        var registry = NewRegistry(); string channel = "sdk-" + Guid.NewGuid().ToString("N");
        var setting = new LinOpenSetting { Channel = $"\"{channel}\"", ConnectionName = "\"LIN1\"", TimeoutMs = 5000 };
        IDisposable? lease = null; LinConnectionResource? retiring = null;
        var old = new GatedDriver { BlockClose = true };
        if (physicalWait)
        {
            retiring = new LinConnectionResource(old, LinConnectionResource.PhysicalKey(LinAdapterType.NI, channel));
            retiring.Dispose(); await old.CloseEntered.Task.WaitAsync(guard.Token);
        }
        else lease = await LinRegistryGuard.Enter(registry, guard.Token);
        int factories = 0;
        var executor = new LinOpenExecutor(_ => { Interlocked.Increment(ref factories); return new GatedDriver(); });
        var obsolete = executor.ExecuteAsync(Context(registry, setting), guard.Token);
        Assert.False(obsolete.IsCompleted);
        Cleanup(registry, "CleanupExecution");
        old.Release.Set(); lease?.Dispose();
        try
        {
            Assert.Equal(TestStatus.Error, (await obsolete.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(0, factories); Assert.False(registry.Contains(LinHelper.GetAdapterKey("LIN1")));
            Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(Context(registry, setting), guard.Token)).StepResult.Status);
            Assert.Equal(1, factories);
        }
        finally
        {
            old.Release.Set(); if (retiring != null) await retiring.CloseAsync(2000, guard.Token);
            if (registry.TryGet<ILinAdapter>(LinHelper.GetAdapterKey("LIN1"), out var current)) await ((LinConnectionResource)current).CloseAsync(2000, guard.Token);
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
        var setting = new LinOpenSetting { Channel = $"\"sdk-{Guid.NewGuid():N}\"", ConnectionName = "\"LIN1\"", TimeoutMs = 5000 };
        var old = new GatedDriver { BlockOpen = true };
        var opening = new LinOpenExecutor(_ => old).ExecuteAsync(Context(registry, setting), caller.Token);
        await old.Entered.Task.WaitAsync(guard.Token);
        caller.Cancel();
        Assert.Equal(TestStatus.Aborted, (await opening.WaitAsync(guard.Token)).StepResult.Status);
        Cleanup(registry, newDebugRegistry ? "CleanupAll" : "CleanupExecution");
        var nextRegistry = newDebugRegistry ? NewRegistry() : registry;
        var next = new GatedDriver(); int created = 0;
        var reopening = new LinOpenExecutor(_ => { Interlocked.Increment(ref created); return next; }).ExecuteAsync(Context(nextRegistry, setting), guard.Token);
        try
        {
            Assert.False(reopening.IsCompleted); Assert.Equal(0, created); Assert.Equal(0, old.CloseCount);
            old.Release.Set();
            Assert.Equal(TestStatus.Completed, (await reopening.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(1, created); Assert.Equal(1, old.CloseCount);
            Assert.True(nextRegistry.TryGet<ILinAdapter>(LinHelper.GetAdapterKey("LIN1"), out var current));
            Assert.True(current.IsConnected);
            if (newDebugRegistry) Cleanup(registry, "CleanupAll");
            Assert.True(current.IsConnected); Assert.Equal(0, next.CloseCount);
            await ((LinConnectionResource)current).CloseAsync(2000, guard.Token);
        }
        finally { old.Release.Set(); await reopening.WaitAsync(guard.Token); Cleanup(nextRegistry, "CleanupAll"); }
    }

    [Fact]
    public async Task CancellationInSuccessfulOpenLogDoesNotReturnCompleted()
    {
        var registry = NewRegistry(); using var caller = new CancellationTokenSource();
        var context = Context(registry, new LinOpenSetting { Channel = $"\"sdk-{Guid.NewGuid():N}\"", ConnectionName = "\"LIN1\"" });
        ((SdkLinContext)(object)context).OnLog = text => { if (text.Contains("LIN 通道已打开")) caller.Cancel(); };
        try
        {
            var result = await new LinOpenExecutor(_ => new GatedDriver()).ExecuteAsync(context, caller.Token);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            if (registry.TryGet<ILinAdapter>(LinHelper.GetAdapterKey("LIN1"), out var cancelledOwner)) Assert.False(cancelledOwner.IsConnected);
        }
        finally
        {
            if (registry.TryGet<ILinAdapter>(LinHelper.GetAdapterKey("LIN1"), out var current)) await ((LinConnectionResource)current).CloseAsync();
            Cleanup(registry, "CleanupAll");
        }
    }

    private static IExecutionContext Context(IResourceRegistry registry, LinOpenSetting setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkLinContext>();
        var state = (SdkLinContext)(object)context; state.Registry = registry; state.Plugin = new LinOpenPlugin(); state.Setting = setting; return context;
    }
    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry, string method) => registry.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
    private sealed class GatedDriver : ILinAdapter
    {
        internal bool BlockOpen, BlockClose; internal int CloseCount;
        internal readonly ManualResetEventSlim Release = new();
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CloseEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsConnected { get; private set; }
        public void Open(LinAdapterConfig config) { Entered.TrySetResult(); if (BlockOpen && !Release.Wait(10000)) throw new TimeoutException("test open barrier"); IsConnected = true; }
        public void Close() { CloseEntered.TrySetResult(); if (BlockClose && !Release.Wait(10000)) throw new TimeoutException("test close barrier"); CloseCount++; IsConnected = false; }
        public void Dispose() => Close();
        public void Write(LinFrame frame) { }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => null;
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => null;
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { }
    }
}

public class SdkLinContext : LinContext
{
    public IResourceRegistry Registry { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Resources" ? Registry : base.Invoke(method, args);
}

public class LookupLinRegistry : DispatchProxy
{
    public IResourceRegistry Inner = null!; public string Key = ""; public Action? BeforeLookup;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet" && Equals(args?[0], Key)) { var action = BeforeLookup; BeforeLookup = null; action?.Invoke(); }
        return method.Invoke(Inner, args);
    }
}
