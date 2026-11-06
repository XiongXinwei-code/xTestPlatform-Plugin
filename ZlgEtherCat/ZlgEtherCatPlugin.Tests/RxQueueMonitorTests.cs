using System.Diagnostics;
using ZlgEtherCat.Runtime;
using Xunit;

namespace ZlgEtherCat.Tests;

public sealed class RxQueueMonitorTests
{
    private static long At(int milliseconds) => checked(Stopwatch.Frequency * milliseconds / 1000);

    private static RxQueueMonitor Start(uint capacity = 10, int high = 80, int backlog = 100, int repeat = 1000, bool latest = false)
    {
        var monitor = new RxQueueMonitor();
        monitor.Reset(true, capacity, high, backlog, repeat, latest, At(0));
        return monitor;
    }

    [Fact]
    public void HighWatermarkUsesCeilingAndCountsEpisodesWhilePeakAndFullObservationsAccumulate()
    {
        var monitor = Start(capacity: 3, high: 80);
        monitor.ObserveDepth(2, At(1));
        var before = monitor.Read(At(1));
        Assert.Equal(3u, before.HighWatermarkDepth);
        Assert.False(before.HighWatermarkActive);
        Assert.Null(monitor.TryTakeNotice(At(1)));
        monitor.ObserveDepth(3, At(2));
        var first = monitor.TryTakeNotice(At(2));
        Assert.NotNull(first);
        Assert.True(first!.Status.HighWatermarkActive);
        Assert.True(first.Status.OverflowSuspected);
        Assert.Equal(1, first.Status.HighWatermarkEvents);
        monitor.ObserveDepth(4, At(3));
        monitor.ObserveDepth(2, At(4));
        monitor.ObserveDepth(3, At(5));
        var status = monitor.Read(At(5));
        Assert.Equal(4u, status.PeakDepth);
        Assert.Equal(3u, status.CurrentDepth);
        Assert.Equal(2, status.HighWatermarkEvents);
        Assert.Equal(3, status.FullQueueObservations);
        Assert.Equal(At(4), status.LastRecoveryTimestamp);
        Assert.True(status.OverflowSuspected);
        Assert.False(status.HardwareDroppedFramesAvailable);
        Assert.Null(status.HardwareDroppedFrames);
    }

    [Fact]
    public void BacklogRequiresAContinuousObservedNonzeroEpisodeAndReadingDoesNotInventEvidence()
    {
        var monitor = Start();
        monitor.ObserveDepth(1, At(10));
        Assert.False(monitor.Read(At(200)).BacklogWarningActive);
        monitor.ObserveDepth(1, At(109));
        Assert.False(monitor.Read(At(109)).BacklogWarningActive);
        monitor.ObserveDepth(2, At(110));
        Assert.True(monitor.Read(At(110)).BacklogWarningActive);
        Assert.Equal(1, monitor.Read(At(110)).BacklogWarningEvents);
        monitor.ObserveDepth(1, At(500));
        Assert.Equal(1, monitor.Read(At(500)).BacklogWarningEvents);
        monitor.ObserveDepth(0, At(501));
        var recovered = monitor.Read(At(501));
        Assert.False(recovered.BacklogWarningActive);
        Assert.Equal(At(501), recovered.LastRecoveryTimestamp);
        monitor.ObserveDepth(1, At(900));
        monitor.ObserveDepth(1, At(999));
        Assert.False(monitor.Read(At(999)).BacklogWarningActive);
        monitor.ObserveDepth(1, At(1000));
        Assert.Equal(2, monitor.Read(At(1000)).BacklogWarningEvents);
    }

    [Fact]
    public void UnknownCapacityNeverLooksFullAndDirectModeDoesNotProduceQueueAlarms()
    {
        var unknown = Start(capacity: 0);
        unknown.ObserveDepth(uint.MaxValue, At(1));
        unknown.ObserveDepth(uint.MaxValue, At(101));
        var status = unknown.Read(At(101));
        Assert.False(status.Available);
        Assert.True(status.Monitoring);
        Assert.Equal(uint.MaxValue, status.CurrentDepth);
        Assert.False(status.HighWatermarkActive);
        Assert.False(status.OverflowSuspected);
        Assert.Equal(0, status.FullQueueObservations);
        Assert.True(status.BacklogWarningActive);
        Assert.NotNull(unknown.TryTakeNotice(At(101)));

        var direct = new RxQueueMonitor();
        direct.Reset(false, 10, 80, 100, 1000, true, At(0));
        direct.BeginPoll(At(100));
        direct.ObserveDepth(10, At(200));
        direct.ObserveDepth(10, At(400));
        var disabled = direct.Read(At(500));
        Assert.False(disabled.Available);
        Assert.False(disabled.Monitoring);
        Assert.False(disabled.HighWatermarkActive);
        Assert.False(disabled.BacklogWarningActive);
        Assert.False(disabled.OverflowSuspected);
        Assert.Null(disabled.LastObservationTimestamp);
        Assert.Null(direct.TryTakeNotice(At(500)));
    }

    [Fact]
    public void FullQueueSuspicionStaysLatchedAfterDynamicAlarmsRecoverWithoutClaimingDrops()
    {
        var monitor = Start(latest: true);
        monitor.ObserveDepth(10, At(0));
        monitor.ObserveDepth(10, At(100));
        monitor.ObserveDepth(0, At(101));
        var status = monitor.Read(At(201));
        Assert.False(status.HighWatermarkActive);
        Assert.False(status.BacklogWarningActive);
        Assert.True(status.OverflowSuspected);
        Assert.True(status.LatestOnly);
        Assert.Equal(At(101), status.LastRecoveryTimestamp);
        Assert.Equal(100d, status.ObservationAgeMs!.Value, 3);
        Assert.Null(status.HardwareDroppedFrames);
        Assert.False(status.HardwareDroppedFramesAvailable);
        Assert.NotNull(status.LastAlarm);
        Assert.Contains("恢复", monitor.TryTakeNotice(At(201))!.Message);
    }

    [Fact]
    public void NoticesAreImmediateThenThrottledAcrossEpisodesAndPreserveCompressedRecovery()
    {
        var monitor = Start(backlog: 5000);
        monitor.ObserveDepth(8, At(0));
        Assert.NotNull(monitor.TryTakeNotice(At(0)));
        monitor.ObserveDepth(0, At(10));
        monitor.ObserveDepth(9, At(20));
        Assert.Null(monitor.TryTakeNotice(At(999)));
        var latest = monitor.TryTakeNotice(At(1000));
        Assert.NotNull(latest);
        Assert.Equal(9u, latest!.Status.CurrentDepth);
        Assert.Equal(2, latest.Status.HighWatermarkEvents);
        Assert.Equal(At(10), latest.Status.LastRecoveryTimestamp);
        Assert.Null(monitor.TryTakeNotice(At(1999)));
        Assert.NotNull(monitor.TryTakeNotice(At(2000)));
        monitor.ObserveDepth(0, At(2001));
        Assert.Null(monitor.TryTakeNotice(At(2999)));
        var recovery = monitor.TryTakeNotice(At(3000));
        Assert.NotNull(recovery);
        Assert.Contains("恢复", recovery!.Message);
        Assert.Equal(At(2001), recovery.Status.LastRecoveryTimestamp);
        Assert.Null(monitor.TryTakeNotice(At(10000)));
    }

    [Fact]
    public void StopRetainsTheLastDiagnosticWithoutInventingRecoveryAndResetStartsANewRun()
    {
        var monitor = Start();
        monitor.BeginPoll(At(25));
        monitor.ObserveDepth(10, At(25));
        monitor.RecordNotificationFailure(new IOException("notification unavailable"));
        monitor.Stop();
        monitor.ObserveDepth(0, At(30));
        monitor.BeginPoll(At(1000));
        var stopped = monitor.Read(At(1025));
        Assert.False(stopped.Monitoring);
        Assert.True(stopped.HighWatermarkActive);
        Assert.True(stopped.OverflowSuspected);
        Assert.Equal(10u, stopped.CurrentDepth);
        Assert.Null(stopped.LastRecoveryTimestamp);
        Assert.Equal(25d, stopped.MaxPollGapMs, 3);
        Assert.Equal(1000d, stopped.ObservationAgeMs!.Value, 3);
        Assert.Contains("notification unavailable", stopped.NotificationError);
        Assert.Null(monitor.TryTakeNotice(At(10000)));

        monitor.Reset(true, 20, 75, 200, 500, false, At(10000));
        var reset = monitor.Read(At(10000));
        Assert.True(reset.Monitoring);
        Assert.Equal(20u, reset.Capacity);
        Assert.Equal(15u, reset.HighWatermarkDepth);
        Assert.Equal(0u, reset.PeakDepth);
        Assert.Equal(0, reset.HighWatermarkEvents);
        Assert.False(reset.OverflowSuspected);
        Assert.Null(reset.LastObservationTimestamp);
        Assert.Null(reset.ObservationAgeMs);
        Assert.Null(reset.NotificationError);
        monitor.ObserveDepth(15, At(10001));
        Assert.NotNull(monitor.TryTakeNotice(At(10001)));
    }

    [Fact]
    public void PollGapAndObservationAgeAreSeparateAndSnapshotReadsDoNotRefreshEither()
    {
        var monitor = Start();
        monitor.BeginPoll(At(10));
        monitor.ObserveDepth(1, At(11));
        monitor.BeginPoll(At(40));
        monitor.BeginPoll(At(45));
        var before = monitor.Read(At(50));
        Assert.Equal(30d, before.MaxPollGapMs, 3);
        Assert.Equal(39d, before.ObservationAgeMs!.Value, 3);
        var after = monitor.Read(At(100));
        Assert.Equal(At(11), after.LastObservationTimestamp);
        Assert.Equal(89d, after.ObservationAgeMs!.Value, 3);
        Assert.Equal(30d, after.MaxPollGapMs, 3);
    }

    [Fact]
    public void NotificationFailureIsBoundedVisibleAndRetriedWithoutBypassingThrottle()
    {
        var monitor = Start();
        monitor.ObserveDepth(8, At(0));
        Assert.NotNull(monitor.TryTakeNotice(At(0)));
        monitor.ObserveDepth(0, At(1));
        Assert.NotNull(monitor.TryTakeNotice(At(1000)));
        monitor.RecordNotificationFailure(new IOException("notification failed " + new string('x', 10000)));
        var status = monitor.Read(At(1001));
        Assert.StartsWith("System.IO.IOException: notification failed", status.NotificationError);
        Assert.InRange(status.NotificationError!.Length, 1, 2048);
        Assert.Null(monitor.TryTakeNotice(At(1999)));
        Assert.Contains("恢复", monitor.TryTakeNotice(At(2000))!.Message);
    }

    [Fact]
    public void InvalidConfigurationCannotEraseExistingDiagnosticsAndLargeCapacityCannotOverflowThreshold()
    {
        var monitor = Start();
        monitor.ObserveDepth(10, At(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.Reset(true, 10, 0, 100, 1000, false, At(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.Reset(true, 10, 101, 100, 1000, false, At(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.Reset(true, 10, 80, 0, 1000, false, At(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.Reset(true, 10, 80, 100, 0, false, At(2)));
        Assert.True(monitor.Read(At(2)).OverflowSuspected);
        monitor.Reset(true, uint.MaxValue, 100, 100, 1000, false, At(3));
        Assert.Equal(uint.MaxValue, monitor.Read(At(3)).HighWatermarkDepth);
    }

    [Fact]
    public async Task ConcurrentObservationsAndReadsRemainConsistentAndCountEveryFullObservation()
    {
        var monitor = Start(backlog: 100000);
        var writers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++) monitor.ObserveDepth(10, At(1));
        })).ToArray();
        var reader = Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                var status = monitor.Read(At(2));
                Assert.True(status.PeakDepth >= status.CurrentDepth);
                Assert.True(status.FullQueueObservations >= status.HighWatermarkEvents);
                Assert.Null(status.HardwareDroppedFrames);
                monitor.TryTakeNotice(At(2));
            }
        });
        await Task.WhenAll(writers.Append(reader));
        var final = monitor.Read(At(3));
        Assert.Equal(4000, final.FullQueueObservations);
        Assert.Equal(1, final.HighWatermarkEvents);
        Assert.True(final.OverflowSuspected);
    }
}
