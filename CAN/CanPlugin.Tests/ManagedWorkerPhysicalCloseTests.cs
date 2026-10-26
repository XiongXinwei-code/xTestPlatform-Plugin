using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;
using CAN.Executors;
using CAN.Helpers;
using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class ManagedWorkerPhysicalCloseTests
{
    [Fact]
    public async Task First_open_is_not_held_by_old_close_waiting_for_managed_expression()
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        string channel = Random.Shared.Next(10000, int.MaxValue).ToString();
        string key = CanHelper.GetAdapterKey("CAN1");
        var raw = new Driver(); var owner = new CanConnectionResource(raw, CanChannelIdentity.Normalize(CanAdapterType.Kvaser, channel));
        registry.Set(key, owner);
        var entered = Signal(); var release = Signal(); var staleRejected = Signal();
        var job = owner.StartJob("expression", async _ =>
        {
            entered.TrySetResult(); await release.Task;
            try { owner.Write(new() { Id = 0x123, Data = [9] }); }
            catch (InvalidOperationException) { staleRejected.TrySetResult(); }
        });
        var closeContext = Context(registry, new CanClosePlugin(), new CanCloseSetting { ConnectionName = "\"CAN1\"", TimeoutMs = 6000 });
        Task<ExecutionResult>? closing = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            closing = new CanCloseExecutor().ExecuteAsync(closeContext);
            await owner.ClosePhysicalAsync(1000);
            Assert.False(closing.IsCompleted); Assert.False(job.Completion.IsCompleted);
            var nextRaw = new Driver();
            var opened = await new CanOpenExecutor(_ => nextRaw).ExecuteAsync(Context(registry, new CanOpenPlugin(),
                new CanOpenSetting { AdapterType = CanAdapterType.Kvaser, ConnectionName = "\"CAN1\"", Channel = $"\"{channel}\"", TimeoutMs = 1000 }));
            Assert.Equal(TestStatus.Completed, opened.StepResult.Status); Assert.False(closing.IsCompleted);
            Assert.True(registry.TryGet<ICanAdapter>(key, out var next));
            release.TrySetResult();
            Assert.Equal(TestStatus.Completed, (await closing).StepResult.Status);
            await staleRejected.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(registry.TryGet<ICanAdapter>(key, out var afterClose)); Assert.Same(next, afterClose);
            Assert.Equal(0, raw.Writes); Assert.Equal(0, nextRaw.Writes);
        }
        finally
        {
            release.TrySetResult(); await job.Completion;
            if (closing != null) await closing;
            if (registry.TryGet<ICanAdapter>(key, out var remaining)) await ((CanConnectionResource)remaining).CloseAsync();
        }
    }

    private static IExecutionContext Context(IResourceRegistry registry, xTestPlatform.Core.Plugins.Contracts.IStepPlugin plugin, object setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        var state = (SdkCanContext)(object)context; state.Registry = registry; state.Plugin = plugin; state.Setting = setting;
        return context;
    }

    [Fact]
    public async Task Frozen_managed_expression_does_not_hold_released_driver_or_write_to_next_run()
    {
        var entered = Signal(); var managedExit = Signal(); var staleRejected = Signal();
        string physical = Guid.NewGuid().ToString();
        var raw = new Driver(); var owner = new CanConnectionResource(raw, physical);
        var job = owner.StartJob("expression", async _ =>
        {
            entered.TrySetResult(); await managedExit.Task;
            try { owner.Write(new() { Id = 0x123, Data = [1] }); }
            catch (InvalidOperationException) { staleRejected.TrySetResult(); }
        });
        CanConnectionResource? next = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(1000));
            Assert.Contains("后台", error.Message);
            Assert.True(owner.IsClosed); Assert.Equal(1, raw.Closes);
            Assert.False(job.Completion.IsCompleted);
            await Assert.ThrowsAsync<TimeoutException>(() => job.StopAsync(40, default));
            var nextRaw = new Driver(); next = new CanConnectionResource(nextRaw, physical);
            await next.WriteAsync(new() { Id = 0x124, Data = [2] }, 1000);
            managedExit.TrySetResult(); await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            await staleRejected.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0, raw.Writes); Assert.Equal(1, nextRaw.Writes);
        }
        finally
        {
            managedExit.TrySetResult(); await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { }
            if (next != null) await next.CloseAsync();
        }
    }

    [Fact]
    public async Task Actual_native_write_still_blocks_final_close_and_physical_reopen()
    {
        var raw = new Driver { BlockWrite = true };
        string physical = Guid.NewGuid().ToString();
        var owner = new CanConnectionResource(raw, physical);
        var job = owner.StartJob("native", _ => Task.Run(() => owner.Write(new() { Id = 0x123 })));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(40));
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.Closes);
            Assert.Throws<InvalidOperationException>(() => new CanConnectionResource(new Driver(), physical));
            Assert.False(job.Completion.IsCompleted);
        }
        finally
        {
            raw.WriteExit.TrySetResult(); await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            await owner.CloseAsync();
        }
    }

    [Fact]
    public async Task Real_cancellation_callback_still_blocks_driver_release_after_worker_exit()
    {
        var workerEntered = Signal(); var callbackEntered = Signal(); var callbackExit = Signal();
        var raw = new Driver(); var owner = new CanConnectionResource(raw);
        CancellationTokenRegistration registration = default;
        var job = owner.StartJob("callback", token =>
        {
            registration = token.Register(() => { callbackEntered.TrySetResult(); callbackExit.Task.GetAwaiter().GetResult(); });
            workerEntered.TrySetResult(); return Task.CompletedTask;
        });
        try
        {
            await workerEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var closing = owner.CloseAsync(40);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => closing);
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.Closes);
            Assert.False(job.Completion.IsCompleted);
        }
        finally
        {
            callbackExit.TrySetResult(); await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            await owner.CloseAsync(); registration.Dispose();
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class Driver : ICanAdapter
    {
        internal readonly TaskCompletionSource WriteEntered = Signal(), WriteExit = Signal();
        internal int Closes, Writes;
        internal bool BlockWrite;
        public bool IsConnected { get; private set; } = true;
        public void Open(CanAdapterConfig config) => IsConnected = true;
        public void Close() { Interlocked.Increment(ref Closes); IsConnected = false; }
        public void Dispose() => IsConnected = false;
        public void Write(CanMessage message)
        {
            Interlocked.Increment(ref Writes); WriteEntered.TrySetResult();
            if (BlockWrite) WriteExit.Task.GetAwaiter().GetResult();
        }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
    }
}
