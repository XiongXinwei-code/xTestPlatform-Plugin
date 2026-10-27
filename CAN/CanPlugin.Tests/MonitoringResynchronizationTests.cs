using System.Diagnostics;
using System.Reflection;
using CAN.Runtime;
using Xunit;
using CAN.Models;
using CAN.CANopen.Cyclic;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class MonitoringResynchronizationTests
{
    [Fact]
    public void Old_named_map_uses_the_authorized_new_monitor_defaults_and_keeps_other_defaults()
    {
        var bytes = new System.Buffers.ArrayBufferWriter<byte>(); var writer = new MessagePack.MessagePackWriter(bytes);
        writer.WriteMapHeader(1); writer.Write("MonitoringPeriodMs"); writer.Write(10); writer.Flush();
        var setting = (CyclicSetting)new ServiceStartPlugin().CreateSerializer().Deserialize(bytes.WrittenSpan.ToArray(), 1);
        Assert.True(setting.ResynchronizeLateMonitoring); Assert.Equal(3, setting.MonitoringConsecutiveLatenessLimit);
        Assert.False(setting.RequireOnBusConfirmation); Assert.Equal(100, setting.MaxLatenessMs);
    }

    [Fact]
    public async Task Repeated_and_concurrent_close_do_not_erase_the_recorded_shutdown_history()
    {
        var owner = new CanConnectionResource(new ConfirmedAdapter());
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 1000));
            await Until(() => service.Snapshot().Cycle.CompletedCycles >= 1);
            service.RecordSendFailure(new TimeoutException("retained schedule fault")); service.RequestStop();
            await Until(() => service.Snapshot().Cycle.HostExited);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseAsync());
            Assert.True(service.IsClosed);
            string? history = service.Snapshot().ShutdownHistory; Assert.Contains("retained schedule fault", history);
            await Task.WhenAll(service.CloseAsync(), service.CloseAsync());
            Assert.Equal(history, service.Snapshot().ShutdownHistory); Assert.Null(service.Snapshot().CurrentCloseError);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Default_monitoring_skips_one_late_start_without_sending_or_faulting()
    {
        var raw = new ConfirmedAdapter(); var owner = new CanConnectionResource(raw);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        var tick = (SemaphoreSlim)typeof(CanopenCyclicService).GetField("_tickGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        var due = typeof(CanopenCyclicService).GetField("_nextSync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await tick.WaitAsync();
        bool held = true;
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 10));
            long forced = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            due.SetValue(service, forced);
            typeof(CanopenCyclicService).GetField("_monitorDelayStartedAt", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(service, forced - 10 * Stopwatch.Frequency);
            tick.Release(); held = false;
            using var guard = new CancellationTokenSource(5000);
            while ((long)due.GetValue(service)! == forced && service.Snapshot().Cycle.Error == null) await Task.Delay(2, guard.Token);
            Assert.Null(service.Snapshot().Cycle.Error); Assert.False(service.Snapshot().Cycle.HostExited);
            Assert.True((long)due.GetValue(service)! > Stopwatch.GetTimestamp());
            Assert.Empty(raw.Sent);
        }
        finally { if (held) tick.Release(); try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    private static SemaphoreSlim Tick(CanopenCyclicService service) => (SemaphoreSlim)typeof(CanopenCyclicService).GetField("_tickGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
    private static async Task ForceDue(CanopenCyclicService service, string field, bool late)
    {
        var tick = Tick(service); await tick.WaitAsync();
        try
        {
            long due = Stopwatch.GetTimestamp() - (late ? 5 * Stopwatch.Frequency : 0);
            typeof(CanopenCyclicService).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, due);
            if (field == "_nextSync") typeof(CanopenCyclicService).GetField("_monitorDelayStartedAt", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(service, due - 10 * Stopwatch.Frequency);
            typeof(CanopenCyclicService).GetMethod("WakeSchedule", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null);
        }
        finally { tick.Release(); }
    }
    private static async Task Until(Func<bool> ready)
    { using var guard = new CancellationTokenSource(5000); while (!ready()) await Task.Delay(2, guard.Token); }
    private static async Task Close(CanConnectionResource owner)
    { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }

    [Fact]
    public async Task Starting_motion_interrupts_a_long_monitor_delay_without_waiting_the_old_period()
    {
        var targetEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new ConfirmedAdapter { OnWrite = frame => { if (frame.Id == 0x201) targetEntered.TrySetResult(); } };
        var owner = new CanConnectionResource(raw);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 1000));
            await Until(() => service.Snapshot().Cycle.CompletedCycles >= 1);
            await Until(() => service.Snapshot().Cycle.MonitoringDelayPending);
            service.StartConfiguredTargets([1], [new(0x201, [1])], new(0x80, [0]), new(10000, 1000, 1000, true, 60000),
                new([new(1, 0x6040, 0, [0])], 1000));
            await targetEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Null(service.Snapshot().Cycle.Error);
        }
        finally { owner.Isolate("software fixture teardown, not a device stop"); await Close(owner); }
    }

    [Fact]
    public async Task Monitoring_interval_is_local_to_its_own_start_not_motion_start_or_cumulative_grid()
    {
        var owner = new CanConnectionResource(new ConfirmedAdapter());
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 1000));
            await Until(() => service.Snapshot().Cycle.CompletedCycles >= 1);
            typeof(CanopenCyclicService).GetField("_motionStart", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(service, Stopwatch.GetTimestamp() - 1000 * Stopwatch.Frequency);
            long before = service.Snapshot().Cycle.CompletedCycles;
            await ForceDue(service, "_nextSync", false);
            await Until(() => service.Snapshot().Cycle.CompletedCycles > before);
            var cycle = service.Snapshot().Cycle;
            Assert.Equal("IndependentStartToStart", cycle.MonitoringTimingMode);
            Assert.Equal(10000, cycle.MonitoringDelayTargetMs);
            Assert.True(cycle.LastMonitoringWaitMs >= 10000 && cycle.LastMonitoringWaitMs < 20000);
            Assert.Equal(Math.Max(0, cycle.LastMonitoringWaitMs!.Value - 10000), cycle.LastMonitoringWaitExcessMs);
            Assert.Null(cycle.Error); Assert.Equal(0, cycle.SkippedMonitoringTicks);
        }
        finally { await Close(owner); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Default_third_consecutive_lateness_faults_but_explicit_strict_mode_faults_first(bool resynchronize)
    {
        var raw = new ConfirmedAdapter(); var owner = new CanConnectionResource(raw);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 1000) { ResynchronizeLateMonitoring = resynchronize });
            // The initial on-time frame may complete; subsequent due times are controlled.
            await Until(() => service.Snapshot().Cycle.CompletedCycles >= 1);
            long completed = service.Snapshot().Cycle.CompletedCycles;
            int count = resynchronize ? 3 : 1;
            for (int n = 1; n <= count; n++)
            {
                await ForceDue(service, "_nextSync", true);
                await Until(() => service.Snapshot().Cycle.Error != null || service.Snapshot().Cycle.SkippedMonitoringTicks >= n);
                if (n < count) Assert.Null(service.Snapshot().Cycle.Error);
            }
            await Until(() => service.Snapshot().Cycle.HostExited);
            var cycle = service.Snapshot().Cycle;
            Assert.NotNull(cycle.Error); Assert.Equal("MonitoringStartLateness", cycle.FailurePhase);
            Assert.Equal(completed, cycle.CompletedCycles); Assert.Null(owner.IsolationReason);
            Assert.Equal(resynchronize ? 3 : 0, cycle.SkippedMonitoringTicks);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task An_on_time_monitoring_start_resets_the_consecutive_lateness_counter()
    {
        var owner = new CanConnectionResource(new ConfirmedAdapter());
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 1000));
            await Until(() => service.Snapshot().Cycle.CompletedCycles >= 1);
            await ForceDue(service, "_nextSync", true);
            await Until(() => service.Snapshot().Cycle.SkippedMonitoringTicks == 1);
            Assert.Equal(1, service.Snapshot().Cycle.ConsecutiveLateMonitoringStarts);
            long completed = service.Snapshot().Cycle.CompletedCycles;
            await ForceDue(service, "_nextSync", false);
            await Until(() => service.Snapshot().Cycle.CompletedCycles > completed);
            Assert.Equal(0, service.Snapshot().Cycle.ConsecutiveLateMonitoringStarts);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Motion_start_lateness_stays_strict_with_monitor_resynchronization_defaults()
    {
        var owner = new CanConnectionResource(new ConfirmedAdapter());
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(10000, 1000, 1000, true, 60000));
            await ForceDue(service, "_nextTarget", true);
            await Until(() => service.Snapshot().Cycle.HostExited);
            Assert.Contains("周期起始迟到超限", service.Snapshot().Cycle.Error);
            Assert.Equal("TargetStartLateness", service.Snapshot().Cycle.FailurePhase);
            Assert.Equal(0, service.Snapshot().Cycle.SkippedMonitoringTicks);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Monitoring_Sync_is_still_strict_when_an_active_motion_owns_the_schedule()
    {
        var raw = new ConfirmedAdapter(); var owner = new CanConnectionResource(raw);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        var tick = Tick(service); await tick.WaitAsync(); bool held = true;
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 1000));
            // Inject active-motion scheduler state, without sending control/SDO.
            void Set(string field, object value) => typeof(CanopenCyclicService).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, value);
            Set("_moving", true); Set("_publishedAt", Stopwatch.GetTimestamp());
            Set("_options", new CycleOptions(InputMaxAgeMs: 60000));
            Set("_nextTarget", long.MaxValue); Set("_nextSync", Stopwatch.GetTimestamp() - 5 * Stopwatch.Frequency);
            tick.Release(); held = false;
            await Until(() => service.Snapshot().Cycle.HostExited);
            Assert.Contains("周期起始迟到超限", service.Snapshot().Cycle.Error);
            Assert.Equal("MonitoringStartLateness", service.Snapshot().Cycle.FailurePhase);
            Assert.Equal(0, service.Snapshot().Cycle.SkippedMonitoringTicks); Assert.Empty(raw.Sent);
        }
        finally { if (held) tick.Release(); await Close(owner); }
    }

    [Fact]
    public async Task Host_submission_records_adapter_calls_without_inventing_native_confirmation()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 1000) { RequireOnBusConfirmation = false });
            await Until(() => service.Snapshot().Cycle.CompletedCycles >= 1);
            var batch = service.Snapshot().Cycle.Timing!.Batch!;
            var frame = Assert.Single(batch.Submissions);
            Assert.Equal((uint)0x80, frame.CobId); Assert.Equal(1, frame.Dlc); Assert.Equal("00", frame.DataHex);
            Assert.True(frame.CallSucceeded); Assert.True(frame.ReturnedMonotonicTicks >= frame.StartedMonotonicTicks);
            Assert.Equal("AdapterCallsReturned", batch.SubmissionState); Assert.True(batch.AdapterCallExited);
            Assert.Null(batch.NativeExited); Assert.Null(batch.NativeCallCount); Assert.Null(batch.KnownAcceptedFrames);
            Assert.Equal(0, batch.ConfirmedFrames);
            batch.Submissions[0] = null!;
            Assert.NotNull(service.Snapshot().Cycle.Timing!.Batch!.Submissions[0]);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Submit_only_close_preserves_history_but_does_not_invent_a_current_confirmation_failure()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 1000, 1000) { RequireOnBusConfirmation = false });
            await Until(() => service.Snapshot().Cycle.CompletedCycles >= 1);
            service.RecordSendFailure(new TimeoutException("original lateness evidence")); service.RequestStop();
            await Until(() => service.Snapshot().Cycle.HostExited);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseAsync());
            Assert.Contains("original lateness evidence", error.Message);
            Assert.Contains("original lateness evidence", service.Snapshot().ShutdownHistory);
            Assert.Null(service.Snapshot().CurrentCloseError);
            Assert.True(service.IsClosed); Assert.False(service.Snapshot().Cycle.StopCommandsAcknowledged);
            Assert.True(service.Snapshot().ServiceCleanupCompleted); Assert.False(service.Snapshot().ServiceShutdownConfirmed);
            Assert.Equal(0, service.Snapshot().Cycle.ConfirmedFrames);
        }
        finally { await Close(owner); }
    }
}
