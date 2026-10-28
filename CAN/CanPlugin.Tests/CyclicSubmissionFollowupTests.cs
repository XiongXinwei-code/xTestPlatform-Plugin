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
public sealed class CyclicSubmissionFollowupTests
{
    [Fact]
    public async Task Stop_cancels_a_periodic_write_already_waiting_for_the_transmit_gate()
    {
        using var guard = new CancellationTokenSource(15000);
        var registry = NewRegistry();
        var raw = new QueuedCycleAdapter();
        var owner = new CanConnectionResource(raw);
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        using var releaseGate = new ManualResetEventSlim();
        var holding = HoldTransmitGate(owner, releaseGate);
        await holding.Entered.WaitAsync(guard.Token);
        Task<ExecutionResult>? stopping = null;
        try
        {
            Assert.Equal(TestStatus.Completed, (await Start(registry)).StepResult.Status);
            await raw.BeforeTransmitGate.Task.WaitAsync(guard.Token);
            stopping = new CanCyclicSendStopExecutor().ExecuteAsync(Context(registry, false), guard.Token);
            var job = Job(owner);
            var stop = (CancellationTokenSource)typeof(CanBackgroundJob).GetField("_stop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(job)!;
            while (!stop.IsCancellationRequested) await Task.Delay(1, guard.Token);
            await ((Task)typeof(CanBackgroundJob).GetField("_callbacks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(job)!).WaitAsync(guard.Token);
            // Cancelling a queued unit now removes it without waiting for an
            // unrelated older unit to leave the connection FIFO.
            Assert.Equal(TestStatus.Completed, (await stopping.WaitAsync(guard.Token)).StepResult.Status);
            releaseGate.Set();
            Assert.Equal(TestStatus.Completed, (await stopping.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(0, raw.WriteCount);
        }
        finally
        {
            releaseGate.Set(); await holding.Completion;
            if (stopping != null) await stopping;
            await Close(owner); Cleanup(registry);
        }
    }

    [Fact]
    public async Task Removed_execution_marker_rejects_a_periodic_write_waiting_for_the_transmit_gate()
    {
        using var guard = new CancellationTokenSource(15000);
        var registry = NewRegistry();
        var raw = new QueuedCycleAdapter();
        var owner = new CanConnectionResource(raw);
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        using var releaseGate = new ManualResetEventSlim();
        var holding = HoldTransmitGate(owner, releaseGate);
        await holding.Entered.WaitAsync(guard.Token);
        try
        {
            Assert.Equal(TestStatus.Completed, (await Start(registry)).StepResult.Status);
            await raw.BeforeTransmitGate.Task.WaitAsync(guard.Token);
            var job = Job(owner);
            // The real SDK can remove an execution entry before disposing the
            // remaining owners. Reproduce this specific intermediate state; this
            // is not claimed to execute the complete CleanupExecution flow.
            Assert.True(registry.Remove("PluginCommon.ExecutionEpoch:CAN"));
            Assert.True(owner.IsConnected); // No owner-close flag can hide the missing boundary.
            releaseGate.Set();
            await job.Completion.WaitAsync(guard.Token);
            Assert.Equal(0, raw.WriteCount);
            Assert.NotNull(job.Error);
        }
        finally { releaseGate.Set(); await holding.Completion; await Close(owner); Cleanup(registry); }
    }

    [Fact]
    public async Task Original_job_cancellation_blocks_submission_before_linked_callbacks_are_delivered()
    {
        using var guard = new CancellationTokenSource(15000);
        using var releaseGate = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = NewRegistry();
        var raw = new QueuedCycleAdapter { AfterWrite = releaseCallback.Set };
        var owner = new CanConnectionResource(raw);
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        var holding = HoldTransmitGate(owner, releaseGate);
        await holding.Entered.WaitAsync(guard.Token);
        Task<ExecutionResult>? stopping = null;
        try
        {
            Assert.Equal(TestStatus.Completed, (await Start(registry)).StepResult.Status);
            await raw.BeforeTransmitGate.Task.WaitAsync(guard.Token);
            var job = Job(owner);
            var stop = (CancellationTokenSource)typeof(CanBackgroundJob).GetField("_stop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(job)!;
            using var blockedCallback = stop.Token.Register(() =>
            {
                callbackEntered.TrySetResult();
                if (!releaseCallback.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("test cancellation-delivery barrier");
            });
            // Registered after the linked CTS: LIFO callbacks block delivery to
            // groupStop, while the actual job token already reports cancelled.
            stopping = new CanCyclicSendStopExecutor().ExecuteAsync(Context(registry, false), guard.Token);
            await callbackEntered.Task.WaitAsync(guard.Token);
            Assert.True(stop.IsCancellationRequested);
            releaseGate.Set();
            var actualWork = (Task)typeof(CanBackgroundJob).GetField("_work", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(job)!;
            await actualWork.WaitAsync(guard.Token);
            Assert.Equal(0, raw.WriteCount);
            Assert.False(stopping.IsCompleted); // Do not pretend the blocked cancellation callback has exited.
            releaseCallback.Set();
            Assert.Equal(TestStatus.Completed, (await stopping.WaitAsync(guard.Token)).StepResult.Status);
        }
        finally
        {
            releaseGate.Set(); releaseCallback.Set(); await holding.Completion;
            if (stopping != null) await stopping;
            await Close(owner); Cleanup(registry);
        }
    }

    [Fact]
    public async Task A_successful_start_keeps_its_periodic_job_after_the_start_steps_token_is_cancelled()
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        var registry = NewRegistry();
        var raw = new QueuedCycleAdapter();
        var owner = new CanConnectionResource(raw);
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        using var releaseGate = new ManualResetEventSlim();
        var holding = HoldTransmitGate(owner, releaseGate);
        await holding.Entered.WaitAsync(guard.Token);
        try
        {
            Assert.Equal(TestStatus.Completed, (await Start(registry, caller.Token)).StepResult.Status);
            await raw.BeforeTransmitGate.Task.WaitAsync(guard.Token);
            caller.Cancel();
            releaseGate.Set(); // The first native entry is necessarily after the short token cancellation.
            await raw.Written.Task.WaitAsync(guard.Token);
            Assert.True(raw.WriteCount > 0);
            Assert.Null(Job(owner).Error);
            Assert.Equal(TestStatus.Completed, (await new CanCyclicSendStopExecutor().ExecuteAsync(Context(registry, false), guard.Token)).StepResult.Status);
        }
        finally { releaseGate.Set(); await holding.Completion; await Close(owner); Cleanup(registry); }
    }

    private static Task<ExecutionResult> Start(IResourceRegistry registry, CancellationToken ct = default)
        => new CanCyclicSendStartExecutor().ExecuteAsync(Context(registry, true), ct);

    private static IExecutionContext Context(IResourceRegistry registry, bool start)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        var state = (SdkCanContext)(object)context;
        state.Registry = registry;
        state.Plugin = start ? new CanCyclicSendStartPlugin() : new CanCyclicSendStopPlugin();
        state.Setting = start ? new CanCyclicSendStartSetting
        {
            TaskName = "\"queued\"",
            Messages = [new() { CanId = "\"0x201\"", Data = "\"01\"", CycleTimeMs = 10 }]
        } : new CanCyclicSendStopSetting { TaskName = "\"queued\"", TimeoutMs = 5000 };
        return context;
    }

    private static CanBackgroundJob Job(CanConnectionResource owner)
        => ((Dictionary<string, CanBackgroundJob>)typeof(CanConnectionResource).GetField("_jobs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!)["queued"];

    private static (Task Entered, Task Completion) HoldTransmitGate(CanConnectionResource owner, ManualResetEventSlim release)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transmitGate = (CanSubmissionGate)typeof(CanConnectionResource).GetField("_txGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        var completion = Task.Run(() =>
        {
            using (transmitGate.Enter(null, default, () => { }, 0))
            {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("test transmit-gate barrier");
            }
        });
        return (entered.Task, completion);
    }

    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(
        typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry) => registry.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
    private static async Task Close(CanConnectionResource owner)
    {
        try { await owner.CloseAsync(5000); }
        catch (Exception) when (owner.IsClosed) { } // Retain historical worker errors; this test still verifies actual release.
    }

    private sealed class QueuedCycleAdapter : ICanAdapter
    {
        private int _checks;
        internal int WriteCount;
        internal Action? AfterWrite;
        internal readonly TaskCompletionSource BeforeTransmitGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Written = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsConnected
        {
            get
            {
                // StartJob performs the first check. WriteCore performs the next
                // immediately before taking the transmit gate. No native I/O runs.
                if (Interlocked.Increment(ref _checks) >= 2) BeforeTransmitGate.TrySetResult();
                return true;
            }
        }
        public void Open(CanAdapterConfig config) { }
        public void Close() { }
        public void Dispose() { }
        public void Write(CanMessage message) { Interlocked.Increment(ref WriteCount); Written.TrySetResult(); AfterWrite?.Invoke(); }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
    }
}
