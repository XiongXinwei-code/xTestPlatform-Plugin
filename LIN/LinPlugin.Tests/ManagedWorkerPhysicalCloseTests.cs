using LIN.Adapters;
using LIN.Models;
using LIN.Runtime;
using PluginCommon;
using LIN.Executors;
using LIN.Helpers;
using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace LIN.Tests;

[CollectionDefinition("LIN managed retirement", DisableParallelization = true)]
public sealed class ManagedRetirementCollection { }

[Collection("LIN managed retirement")]
public sealed class ManagedWorkerPhysicalCloseTests
{
    [Fact]
    public async Task First_open_can_finish_while_old_close_is_waiting_for_managed_log()
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var entered = Signal(); var release = Signal(); var staleRejected = Signal();
        string channel = Guid.NewGuid().ToString(); string key = LinHelper.GetAdapterKey("LIN1");
        var raw = new Driver(); var owner = new LinConnectionResource(raw); owner.Open(new() { Channel = channel });
        registry.Set(key, owner);
        var job = owner.StartJob([async _ =>
        {
            entered.TrySetResult(); await release.Task;
            try { owner.Write(new() { Data = [9] }); }
            catch (InvalidOperationException) { staleRejected.TrySetResult(); }
        }]);
        var closeContext = Context(registry, new LinClosePlugin(), new LinCloseSetting { ConnectionName = "\"LIN1\"", TimeoutMs = 6000 });
        Task<ExecutionResult>? closing = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            closing = new LinCloseExecutor().ExecuteAsync(closeContext);
            await owner.ClosePhysicalAsync(1000);
            Assert.False(closing.IsCompleted); Assert.False(job.Completion.IsCompleted);
            var nextRaw = new Driver();
            var opening = new LinOpenExecutor(_ => nextRaw).ExecuteAsync(Context(registry, new LinOpenPlugin(),
                new LinOpenSetting { ConnectionName = "\"LIN1\"", Channel = $"\"{channel}\"", TimeoutMs = 1000 }));
            Assert.Equal(TestStatus.Completed, (await opening.WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
            Assert.False(closing.IsCompleted);
            Assert.True(registry.TryGet<ILinAdapter>(key, out var replacement));
            var next = Assert.IsType<LinConnectionResource>(replacement);
            release.TrySetResult();
            Assert.Equal(TestStatus.Completed, (await closing).StepResult.Status);
            await staleRejected.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0, raw.Writes); Assert.Equal(0, nextRaw.Writes);
            Assert.True(registry.TryGet<ILinAdapter>(key, out var afterClose)); Assert.Same(next, afterClose);
        }
        finally
        {
            release.TrySetResult(); await job.Completion;
            if (closing != null) await closing;
            if (registry.TryGet<ILinAdapter>(key, out var remaining)) await ((LinConnectionResource)remaining).CloseAsync();
        }
    }

    [Fact]
    public async Task Slow_native_close_of_A_does_not_block_open_of_unrelated_B()
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var raw = new Driver { BlockClose = true }; var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = Guid.NewGuid().ToString() });
        registry.Set(LinHelper.GetAdapterKey("A"), owner);
        var closing = new LinCloseExecutor().ExecuteAsync(Context(registry, new LinClosePlugin(), new LinCloseSetting { ConnectionName = "\"A\"", TimeoutMs = 6000 }));
        try
        {
            await raw.CloseEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var opened = await new LinOpenExecutor(_ => new Driver()).ExecuteAsync(Context(registry, new LinOpenPlugin(),
                new LinOpenSetting { ConnectionName = "\"B\"", Channel = $"\"{Guid.NewGuid()}\"", TimeoutMs = 1000 }));
            Assert.Equal(TestStatus.Completed, opened.StepResult.Status); Assert.False(closing.IsCompleted);
        }
        finally
        {
            raw.CloseExit.TrySetResult(); await closing;
            if (registry.TryGet<ILinAdapter>(LinHelper.GetAdapterKey("B"), out var next)) await ((LinConnectionResource)next).CloseAsync();
        }
    }

    private static IExecutionContext Context(IResourceRegistry registry, xTestPlatform.Core.Plugins.Contracts.IStepPlugin plugin, object setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkLinContext>();
        var state = (SdkLinContext)(object)context; state.Registry = registry; state.Plugin = plugin; state.Setting = setting;
        return context;
    }

    [Fact]
    public async Task Frozen_managed_log_does_not_keep_physical_driver_occupied()
    {
        var logEntered = Signal(); var logExit = Signal(); var staleRejected = Signal();
        string channel = Guid.NewGuid().ToString();
        var raw = new Driver(); var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = channel });
        var job = owner.StartJob([_ => Task.Run(async () =>
        {
            owner.Write(new() { Data = [1] });
            logEntered.TrySetResult(); await logExit.Task;
            try { owner.Write(new() { Data = [9] }); }
            catch (InvalidOperationException) { staleRejected.TrySetResult(); }
        })]);
        LinConnectionResource? next = null;
        try
        {
            await logEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(1000));
            Assert.Contains("后台", error.ToString());
            Assert.True(owner.IsClosed); Assert.Equal(1, raw.Closes);
            Assert.False(job.Completion.IsCompleted);
            await Assert.ThrowsAsync<TimeoutException>(() => job.StopAsync(40));
            await RetiringResources.WaitForPreviousAsync("LIN:NI:" + channel.ToUpperInvariant(), 1000, default);
            var nextRaw = new Driver(); next = new LinConnectionResource(nextRaw); next.Open(new() { Channel = channel });
            next.Write(new() { Data = [2] });
            logExit.TrySetResult(); await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            await staleRejected.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1, raw.Writes); Assert.Equal(1, nextRaw.Writes);
        }
        finally
        {
            logExit.TrySetResult(); await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { }
            if (next != null) await next.CloseAsync();
        }
    }

    [Fact]
    public async Task Native_write_must_exit_before_physical_release()
    {
        var raw = new Driver { BlockWrite = true }; var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = Guid.NewGuid().ToString() });
        var job = owner.StartJob([token => Task.Run(() => owner.Write(new() { Data = [1] }, token, 60000))]);
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(40));
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.Closes); Assert.False(job.Completion.IsCompleted);
        }
        finally { raw.WriteExit.TrySetResult(); await job.Completion; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Actual_cancel_callback_must_exit_even_after_worker_finishes()
    {
        var workerEntered = Signal(); var callbackEntered = Signal(); var callbackExit = Signal();
        var raw = new Driver(); var owner = new LinConnectionResource(raw); owner.Open(new() { Channel = Guid.NewGuid().ToString() });
        CancellationTokenRegistration registration = default;
        var job = owner.StartJob([token =>
        {
            registration = token.Register(() => { callbackEntered.TrySetResult(); callbackExit.Task.GetAwaiter().GetResult(); });
            workerEntered.TrySetResult(); return Task.CompletedTask;
        }]);
        try
        {
            await workerEntered.Task.WaitAsync(TimeSpan.FromSeconds(3)); var closing = owner.CloseAsync(40);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => closing);
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.Closes); Assert.False(job.Completion.IsCompleted);
        }
        finally { callbackExit.TrySetResult(); await job.Completion; await owner.CloseAsync(); registration.Dispose(); }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class Driver : ILinAdapter
    {
        internal readonly TaskCompletionSource WriteEntered = Signal(), WriteExit = Signal(), CloseEntered = Signal(), CloseExit = Signal();
        internal bool BlockWrite, BlockClose;
        internal int Closes, Writes;
        public bool IsConnected { get; private set; }
        public void Open(LinAdapterConfig config) => IsConnected = true;
        public void Close() { CloseEntered.TrySetResult(); if (BlockClose) CloseExit.Task.GetAwaiter().GetResult(); Interlocked.Increment(ref Closes); IsConnected = false; }
        public void Dispose() => IsConnected = false;
        public void Write(LinFrame frame)
        {
            Interlocked.Increment(ref Writes); WriteEntered.TrySetResult();
            if (BlockWrite) WriteExit.Task.GetAwaiter().GetResult();
        }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => null;
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => null;
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { }
    }
}
