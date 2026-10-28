using System.Diagnostics;
using System.Reflection;
using CAN.Adapters.NiXnet;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class BatchLifecycleAdmissionTests
{
    private static CanSubmissionGate Queue(CanConnectionResource owner) => (CanSubmissionGate)typeof(CanConnectionResource)
        .GetField("_txGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static async Task Until(Func<bool> condition)
    { using var guard = new CancellationTokenSource(5000); while (!condition()) await Task.Delay(2, guard.Token); }

    [Fact]
    public async Task Stop_withdraws_a_whole_queued_target_without_waiting_for_an_unrelated_batch()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        IDisposable? turn = null;
        try
        {
            var service = owner.StartService("source", []);
            service.StartMonitoring(new(0x80, [0]), new(60000, 5000, 1000) { RequireOnBusConfirmation = false });
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            turn = Queue(owner).Enter(null, default, () => { }, 0x700);
            service.StartTargets([1], [new(0x201, [7])], new(0x80, [0]), new(60000, 5000, 1000, false, 60000));
            await Until(() => Queue(owner).QueuedCount == 1);
            await service.StopTargetsAsync(1000);
            Assert.False(service.Snapshot().Cycle.MotionActive);
            Assert.Equal(0, Queue(owner).QueuedCount);
            turn.Dispose(); turn = null;
            await owner.StopServiceAsync("source", 5000, default);
            Assert.DoesNotContain(raw.Sent, frame => frame.Id == 0x201);
            Assert.Null(owner.IsolationReason);
        }
        finally { turn?.Dispose(); await Close(owner); }
    }

    [Fact]
    public async Task Stop_finishes_an_already_admitted_Rpdo_Sync_batch_instead_of_splitting_it()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new FakeAdapter { OnWrite = frame => { if (frame.Id == 0x201) { entered.TrySetResult(); release.Wait(5000); } } };
        var owner = new CanConnectionResource(raw);
        try
        {
            var service = owner.StartService("source", []);
            service.StartTargets([1], [new(0x201, [1]), new(0x301, [2])], new(0x80, [0]), new(60000, 5000, 1000, false, 60000));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stop = service.StopTargetsAsync();
            release.Set(); await stop;
            Assert.Equal(new uint[] { 0x201, 0x301, 0x80 }, raw.Sent.Select(frame => frame.Id));
            Assert.False(service.Snapshot().Cycle.PartialCycle); Assert.Null(owner.IsolationReason);
        }
        finally { release.Set(); await Close(owner); }
    }

    [Fact]
    public async Task A_trajectory_point_that_expires_in_the_fifo_is_not_submitted_to_the_driver()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); IDisposable? turn = null;
        try
        {
            var service = owner.StartService("trajectory", []);
            service.StartTrajectory([1], new([new(0, [new(0x201, [0])]), new(10000, [new(0x201, [1])]), new(20000, [new(0x201, [2])])], 30000),
                new(0x80, [0]), new(10000, 5000, 60000, false, 60000) { MissPolicy = TrajectoryMissPolicy.SkipAndCatchUp });
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            turn = Queue(owner).Enter(null, default, () => { }, 0x700);
            await SetDue(service, 10001);
            await Until(() => Queue(owner).QueuedCount == 1);
            Set(service, "_motionStart", Stopwatch.GetTimestamp() - (long)(40000d * Stopwatch.Frequency / 1000));
            turn.Dispose(); turn = null;
            await Until(() => service.Snapshot().Cycle.HostExited);
            Assert.Contains("轨迹总时长", service.Snapshot().Cycle.Error);
            Assert.Single(raw.Sent, frame => frame.Id == 0x201);
            Assert.Equal(1, service.Snapshot().Cycle.ExecutedTrajectoryPoints);
            Assert.False(service.Snapshot().Cycle.TerminalSubmitted);
        }
        finally { turn?.Dispose(); await Close(owner); }
    }

    [Fact]
    public async Task Rejected_diagnostic_start_cannot_probe_or_mutate_an_existing_submit_only_sender()
    {
        var io = new NiBatchEvidenceTests.Driver { EchoCount = 0 };
        var adapter = new NiXnetAdapter(io, 1, 2); var owner = new CanConnectionResource(adapter);
        try
        {
            var existing = owner.StartService("existing", []);
            existing.StartMonitoring(new(0x80, [0]), new(60000, 5000, 1000) { RequireOnBusConfirmation = false });
            await Until(() => existing.Snapshot().Cycle.CompletedCycles == 1);
            var rejected = owner.StartService("diagnostic", []);
            Assert.Throws<InvalidOperationException>(() => rejected.StartMonitoring(new(0x81, [0]), new(60000, 5000, 1000) { RequireOnBusConfirmation = true }));
            Assert.False(io.QueriedAfterWrite.Task.IsCompleted);
            Assert.False((bool)typeof(NiXnetAdapter).GetField("_strictEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!);
            Assert.Null(owner.IsolationReason); Assert.True(existing.Snapshot().Cycle.MonitoringSyncActive);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task A_staged_sender_probe_excludes_legacy_periodic_jobs_before_the_probe_returns()
    {
        var raw = new ProbeDriver(); var owner = new CanConnectionResource(raw);
        var service = owner.StartService("diagnostic", []);
        var starting = Task.Run(() => service.StartMonitoring(new(0x80, [0]), new(60000, 5000, 1000)));
        int jobStarted = 0;
        try
        {
            await raw.ProbeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Throws<InvalidOperationException>(() => owner.StartJob("legacy", _ => { Interlocked.Increment(ref jobStarted); return Task.CompletedTask; }));
            Assert.Equal(0, jobStarted);
        }
        finally { raw.ProbeRelease.Set(); await starting; await Close(owner); }
    }
    private sealed class ProbeDriver : FakeAdapter, ICanConfirmedTransmitter
    {
        internal readonly TaskCompletionSource ProbeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim ProbeRelease = new();
        public void VerifyConfirmationAvailable() { ProbeEntered.TrySetResult(); if (!ProbeRelease.Wait(5000)) throw new TimeoutException("probe fixture"); }
        public void WriteConfirmed(CanMessage frame, CancellationToken ct) => Write(frame);
    }

    private static void Set(CanopenCyclicService source, string name, object value) => typeof(CanopenCyclicService)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(source, value);
    private static async Task SetDue(CanopenCyclicService source, int elapsed)
    {
        var tick = (SemaphoreSlim)typeof(CanopenCyclicService).GetField("_tickGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
        await tick.WaitAsync();
        try
        {
            Set(source, "_motionStart", Stopwatch.GetTimestamp() - (long)(elapsed * Stopwatch.Frequency / 1000d));
            Set(source, "_nextTarget", Stopwatch.GetTimestamp());
            typeof(CanopenCyclicService).GetMethod("WakeSchedule", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, null);
        }
        finally { tick.Release(); }
    }
    private static async Task Close(CanConnectionResource owner)
    { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Multiple_senders_never_interleave_batch_members_and_parent_close_withdraws_waiting_batches(bool closeWhileQueued)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new FakeAdapter { OnWrite = frame => { if (frame.Id == 0x201) { entered.TrySetResult(); release.Wait(5000); } } };
        var owner = new CanConnectionResource(raw);
        try
        {
            var first = owner.StartService("first", []); var second = owner.StartService("second", []);
            first.StartTargets([1], [new(0x201, [1]), new(0x301, [2])], new(0x80, [0]), new(60000, 5000, 1000, false, 60000));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            second.StartTargets([2], [new(0x202, [3]), new(0x302, [4])], new(0x80, [0]), new(60000, 5000, 1000, false, 60000));
            await Until(() => Queue(owner).QueuedCount == 1);
            Task? closing = closeWhileQueued ? owner.CloseAsync() : null;
            if (closeWhileQueued) await Until(() => Queue(owner).QueuedCount == 0);
            release.Set();
            if (closing != null) { await closing; Assert.True(owner.IsClosed); }
            else
            {
                await Until(() => second.Snapshot().Cycle.CompletedCycles == 1);
                await first.StopTargetsAsync(); await second.StopTargetsAsync();
            }
            Assert.Equal(closeWhileQueued ? new uint[] { 0x201, 0x301, 0x80 } : [0x201, 0x301, 0x80, 0x202, 0x302, 0x80],
                raw.Sent.Select(frame => frame.Id));
            Assert.Null(owner.IsolationReason);
        }
        finally { release.Set(); await Close(owner); }
    }

    [Fact]
    public async Task Skip_policy_reselects_latest_due_point_after_fifo_wait_and_keeps_terminal_submission_evidence()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); IDisposable? turn = null;
        try
        {
            var service = owner.StartService("trajectory", []);
            service.StartTrajectory([1], new([new(0, [new(0x201, [0])]), new(10000, [new(0x201, [1])]), new(20000, [new(0x201, [2])])], 60000),
                new(0x80, [0]), new(10000, 5000, 60000, false, 60000) { MissPolicy = TrajectoryMissPolicy.SkipAndCatchUp });
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            turn = Queue(owner).Enter(null, default, () => { }, 0x700);
            await SetDue(service, 10001); await Until(() => Queue(owner).QueuedCount == 1);
            Set(service, "_motionStart", Stopwatch.GetTimestamp() - (long)(20001d * Stopwatch.Frequency / 1000));
            turn.Dispose(); turn = null;
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            Assert.Equal(new byte[] { 0, 2 }, raw.Sent.Where(frame => frame.Id == 0x201).Select(frame => frame.Data[0]));
            Assert.Equal(2, service.Snapshot().Cycle.ExecutedTrajectoryPoints);
            Assert.Equal(1, service.Snapshot().Cycle.SkippedTrajectoryPoints);
            await service.StopTargetsAsync();
            Assert.True(service.Snapshot().Cycle.TerminalSubmitted); Assert.False(service.Snapshot().Cycle.TerminalConfirmed);
            Assert.Null(service.Snapshot().Cycle.Error);
        }
        finally { turn?.Dispose(); await Close(owner); }
    }
}
