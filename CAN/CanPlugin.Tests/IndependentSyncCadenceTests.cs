using System.Diagnostics;
using System.Reflection;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

// Synchronization gates control due times; these are ordering/ownership tests,
// not assertions of Windows or physical CAN millisecond timing accuracy.
[Collection("CAN blocked I/O")]
public sealed class IndependentSyncCadenceTests
{
    [Fact]
    public async Task Target_batch_does_not_postpone_the_background_Sync_due()
    {
        var (owner, service, _) = await Start();
        try
        {
            long due = Read<long>(service, "_nextSync");
            StartMotion(service);
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            Assert.Equal(due, Read<long>(service, "_nextSync"));
            Assert.Null(service.Snapshot().Cycle.Error);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Stopping_motion_preserves_the_existing_background_clock()
    {
        var (owner, service, _) = await Start();
        try
        {
            StartMotion(service);
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            long due = Read<long>(service, "_nextSync");
            await service.StopTargetsAsync();
            Assert.Equal(due, Read<long>(service, "_nextSync"));
            Assert.Equal("Monitoring", service.Snapshot().Cycle.State);
            Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
            Assert.False(service.Snapshot().Cycle.MotorStoppedVerified);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Coincident_target_and_background_due_send_two_Sync_frames_without_interleaving_the_target_batch()
    {
        var (owner, service, raw) = await Start();
        try
        {
            StartMotion(service);
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            int baseline = raw.Sent.Count;
            var gate = Read<SemaphoreSlim>(service, "_tickGate");
            await gate.WaitAsync();
            try
            {
                long due = Stopwatch.GetTimestamp();
                Set(service, "_nextTarget", due); Set(service, "_nextSync", due);
                typeof(CanopenCyclicService).GetMethod("WakeSchedule", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null);
            }
            finally { gate.Release(); }
            await Until(() => service.Snapshot().Cycle.CompletedCycles >= 4);
            Assert.Equal(new uint[] { 0x80, 0x201, 0x80 }, raw.Sent.Skip(baseline).Select(frame => frame.Id));
            Assert.Null(service.Snapshot().Cycle.Error);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Background_interval_is_anchored_to_its_start_and_does_not_add_the_confirmation_duration()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new ConfirmedAdapter { OnWrite = _ => { entered.TrySetResult(); release.Wait(); } };
        var owner = new CanConnectionResource(raw);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 5000, 1000));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            long beforeRelease = Stopwatch.GetTimestamp(); release.Set();
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            long anchor = Read<long?>(service, "_monitorDelayStartedAt")!.Value;
            Assert.True(anchor < beforeRelease, "Background due must use its own start, not native/confirmation completion");
            Assert.Equal(anchor + (long)(10000 * (Stopwatch.Frequency / 1000d)), Read<long>(service, "_nextSync"));
            Assert.Equal("IndependentStartToStart", service.Snapshot().Cycle.MonitoringTimingMode);
        }
        finally { release.Set(); await owner.CloseAsync(); }
    }

    private static async Task<(CanConnectionResource, CanopenCyclicService, ConfirmedAdapter)> Start()
    {
        var raw = new ConfirmedAdapter();
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x601)
                raw.Incoming.Add(new() { Id = 0x581, Data = [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
        };
        var owner = new CanConnectionResource(raw);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        service.StartMonitoring(new(0x80, [0]), new(10000, 5000, 1000));
        await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
        return (owner, service, raw);
    }

    [Fact]
    public async Task First_background_start_after_a_skipped_initial_tick_has_no_previous_interval()
    {
        var owner = new CanConnectionResource(new ConfirmedAdapter());
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        var gate = Read<SemaphoreSlim>(service, "_tickGate");
        await gate.WaitAsync();
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 5000, 1000));
            Set(service, "_nextSync", Stopwatch.GetTimestamp() - 5 * Stopwatch.Frequency);
        }
        finally { gate.Release(); }
        try
        {
            await Until(() => service.Snapshot().Cycle.SkippedMonitoringTicks == 1);
            await ForceBackground(service, late: false);
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            Assert.Null(service.Snapshot().Cycle.LastMonitoringStartIntervalMs);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Real_background_start_interval_does_not_use_the_virtual_resynchronization_anchor()
    {
        var (owner, service, _) = await Start();
        try
        {
            long firstStart = Read<long?>(service, "_monitorDelayStartedAt")!.Value;
            await ForceBackground(service, late: true);
            await Until(() => service.Snapshot().Cycle.SkippedMonitoringTicks == 1);
            long beforeNext = Stopwatch.GetTimestamp();
            await ForceBackground(service, late: false);
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            var interval = service.Snapshot().Cycle.LastMonitoringStartIntervalMs;
            Assert.NotNull(interval);
            Assert.InRange(interval.Value, Stopwatch.GetElapsedTime(firstStart, beforeNext).TotalMilliseconds,
                Stopwatch.GetElapsedTime(firstStart).TotalMilliseconds);
        }
        finally { await owner.CloseAsync(); }
    }

    private static async Task ForceBackground(CanopenCyclicService service, bool late)
    {
        var gate = Read<SemaphoreSlim>(service, "_tickGate"); await gate.WaitAsync();
        try
        {
            long now = Stopwatch.GetTimestamp();
            Set(service, "_nextSync", now - (late ? 5 * Stopwatch.Frequency : 0));
            // Deliberately different from the real last start. A resync anchor
            // may schedule a tick but cannot stand in for a transmitted tick.
            if (!late) Set(service, "_monitorDelayStartedAt", now - 1000 * Stopwatch.Frequency);
            typeof(CanopenCyclicService).GetMethod("WakeSchedule", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null);
        }
        finally { gate.Release(); }
    }

    private static void StartMotion(CanopenCyclicService service) => service.StartConfiguredTargets([1],
        [new(0x201, [1])], new(0x80, [0]), new(10000, 5000, 1000, true, 60000),
        new([new(1, 0x6040, 0, [0])], 2000));

    private static T Read<T>(CanopenCyclicService service, string name)
        => (T)typeof(CanopenCyclicService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
    private static void Set(CanopenCyclicService service, string name, long value)
        => typeof(CanopenCyclicService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, value);
    private static async Task Until(Func<bool> condition)
    {
        using var guard = new CancellationTokenSource(3000);
        while (!condition()) await Task.Delay(2, guard.Token);
    }
}
