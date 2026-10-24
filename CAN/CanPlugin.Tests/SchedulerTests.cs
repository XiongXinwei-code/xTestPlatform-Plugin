using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public class SchedulerTests
{
    [Fact]
    public async Task ConcurrentStartsCannotReplaceWinningStopConfiguration()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes);
        fake.OnWrite = m =>
        {
            if (m.Id == 0x601) fake.Incoming.Add(new() { Id = 0x581, Data = [0x60, m.Data[1], m.Data[2], m.Data[3], 0, 0, 0, 0] });
        };
        async Task<bool> Start(byte value) => await Task.Run(() =>
        {
            try
            {
                service.StartConfiguredTargets([1], [new(0x201, [value])], Sync, new(80, 500, 1000),
                    new([new(1, 0x2000, 0, [(byte)(value + 10)])], 500));
                return true;
            }
            catch (InvalidOperationException) { return false; }
        });
        var won = await Task.WhenAll(Start(1), Start(2)); Assert.Single(won, x => x);
        await Task.Delay(100); await service.StopTargetsAsync();
        var target = fake.Sent.First(m => m.Id == 0x201).Data[0];
        Assert.Equal((byte)(target + 10), fake.Sent.Single(m => m.Id == 0x601).Data[4]);
        await connection.CloseAsync();
    }

    [Fact]
    public async Task PlannedCloseWithFeedbackSupervisionDoesNotReportFeedbackFailure()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes); ConfigureStop(service, fake);
        var sdo = fake.OnWrite;
        long sequence = 0;
        fake.OnWrite = m =>
        {
            sdo?.Invoke(m);
            if (m.Id == 0x80) fake.Incoming.Add(new() { Id = 0x181, Data = [1],
                TimestampNs = Interlocked.Increment(ref sequence), TimestampDomain = "test", ClockId = "test",
                CaptureMonotonicLowerBoundTicks = System.Diagnostics.Stopwatch.GetTimestamp() });
        };
        service.StartMonitoring(Sync, new(20, 500, 1000));
        service.StartTargets([1], [new(0x201, [1])], Sync, new(80, 500, 1000, true, 5000, true, 100));
        await Task.Delay(150); Assert.True(service.Snapshot().Receive.AllValid);
        await connection.CloseAsync(); Assert.Null(service.Snapshot().Cycle.Error);
    }
    private static readonly OrderedFrame Sync = new(0x80, [0]);
    private static readonly FeedbackRoute[] Routes = [new("TPDO1", 0x181, 1, 100)];

    [Fact]
    public async Task PartialCycleNeverResumesMonitoringSync()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes); ConfigureStop(service, fake);
        var original = fake.OnWrite;
        fake.OnWrite = m => { original?.Invoke(m); if (m.Id == 0x201) { entered.Set(); release.Wait(); } };
        service.StartMonitoring(Sync, new(50, 500, 1000));
        service.StartTargets([1], [new(0x201, [1])], Sync, new(500, 1000, 1000));
        Assert.True(entered.Wait(2000));
        try
        {
            using var cts = new CancellationTokenSource(30);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StopTargetsAsync(500, cts.Token));
        }
        finally { release.Set(); }
        await Task.Delay(100);
        var frames = fake.Sent.ToArray();
        var targetIndex = Array.FindIndex(frames, m => m.Id == 0x201);
        Assert.True(targetIndex >= 0);
        Assert.DoesNotContain(frames.Skip(targetIndex + 1), m => m.Id == 0x80);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CloseAsync());
        Assert.True(connection.IsClosed);
    }

    [Fact]
    public async Task ExplicitMonitorSendsOnlySyncAndRemainsAfterTargetStop()
    {
        var fake = new ConfirmedAdapter();
        var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes);
        ConfigureStop(service, fake);
        service.StartMonitoring(Sync, new(20, 500, 1000));
        await Task.Delay(60); Assert.All(fake.Sent, m => Assert.Equal(0x80u, m.Id));
        service.StartTargets([1], [new(0x201, [1])], Sync, new(40, 500, 1000));
        await Task.Delay(90);
        await service.StopTargetsAsync();
        var count = fake.Sent.Count;
        await Task.Delay(60);
        Assert.Equal("Monitoring", service.Snapshot().Cycle.State);
        Assert.True(fake.Sent.Count > count);
        Assert.All(fake.Sent.Skip(count), m => Assert.Equal(0x80u, m.Id));
        await connection.CloseAsync();
    }

    private static void ConfigureStop(CanopenCyclicService service, ConfirmedAdapter fake)
    {
        // Synthetic test object, not a vendor motor control word.
        service.ConfigureControlledStop(new([new(1, 0x2000, 0, [0])], 500));
        fake.OnWrite = m =>
        {
            if (m.Id == 0x601) fake.Incoming.Add(new() { Id = 0x581,
                Data = [0x60, m.Data[1], m.Data[2], m.Data[3], 0, 0, 0, 0] });
        };
    }

    [Fact]
    public async Task ConnectionCloseExecutesOwnerStopBeforeShuttingDownReceiver()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes); ConfigureStop(service, fake);
        service.StartMonitoring(Sync, new(40, 500, 1000));
        service.StartTargets([1], [new(0x201, [1])], Sync, new(80, 500, 1000));
        await Task.Delay(100);
        await connection.CloseAsync();
        Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
        Assert.False(service.Snapshot().Cycle.MotorStoppedVerified);
        Assert.Contains(fake.Sent, f => f.Id == 0x601);
    }

    [Fact]
    public async Task FeedbackLossTriggersOwnerStopWithoutFrontendPolling()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes); ConfigureStop(service, fake);
        service.StartTargets([1], [new(0x201, [1])], Sync, new(40, 500, 1000, true, 5000, true, 50));
        await Task.Delay(200);
        Assert.Contains("反馈失效", service.Snapshot().Cycle.Error!);
        Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
        Assert.Throws<InvalidOperationException>(() => service.PublishTargets([new(0x201, [2])]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CloseAsync());
        Assert.True(connection.IsClosed);
    }

    [Fact]
    public async Task FiniteTableRunsWithoutInputLeaseOrFrontendUpdates()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes);
        var points = new[] { new TimedCanPoint(0, [new(0x201, [1])]), new TimedCanPoint(80, [new(0x201, [2])]) };
        service.StartTrajectory([1], new(points, 160), Sync, new(40, 500, 1000, true, 1));
        points[1].Frames[0].Data[0] = 99;
        await Task.Delay(250);
        Assert.Contains(fake.Sent, m => m.Id == 0x201 && m.Data[0] == 2);
        Assert.DoesNotContain(fake.Sent, m => m.Id == 0x201 && m.Data[0] == 99);
        await service.StopTargetsAsync(); await connection.CloseAsync();
    }

    [Fact]
    public async Task NonPeriodAlignedTablePointsAreNotSilentlySkipped()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes);
        service.StartTrajectory([1], new([
            new(0, [new(0x201, [1])]), new(75, [new(0x201, [2])]), new(150, [new(0x201, [3])])], 250),
            Sync, new(100, 500, 1000, true, 1));
        await Task.Delay(350);
        Assert.Equal(new byte[] { 1, 2, 3 }, fake.Sent.Where(m => m.Id == 0x201).Select(m => m.Data[0]));
        await service.StopTargetsAsync(); await connection.CloseAsync();
    }

    [Fact]
    public async Task MissedTrajectoryPointFaultsWithoutBurstCatchup()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes);
        fake.OnWrite = frame => { if (frame.Id == 0x201) Thread.Sleep(100); };
        service.StartTrajectory([1], new([new(0, [new(0x201, [1])]), new(40, [new(0x201, [2])])], 300),
            Sync, new(200, 500, 1000));
        await Task.Delay(250);
        Assert.Contains("禁止密集补发", service.Snapshot().Cycle.Error!);
        Assert.DoesNotContain(fake.Sent, m => m.Id == 0x201 && m.Data[0] == 2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CloseAsync());
    }

    [Fact]
    public async Task MonitoringCanRestartTargetsOnlyAfterAcknowledgedStop()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var service = connection.StartService("test", Routes); ConfigureStop(service, fake);
        service.StartMonitoring(Sync, new(40, 500, 1000));
        service.StartTargets([1], [new(0x201, [1])], Sync, new(80, 500, 1000));
        await Task.Delay(100); await service.StopTargetsAsync();
        service.StartTargets([1], [new(0x201, [2])], Sync, new(80, 500, 1000));
        await Task.Delay(100); await service.StopTargetsAsync();
        Assert.Contains(fake.Sent, m => m.Id == 0x201 && m.Data[0] == 2);
        await connection.CloseAsync();
    }

    [Fact]
    public async Task SecondSyncSchedulerCannotStart()
    {
        var fake = new ConfirmedAdapter(); var connection = new CanConnectionResource(fake);
        var first = connection.StartService("one", Routes); var second = connection.StartService("two", Routes);
        first.StartMonitoring(Sync, new(20, 500, 1000));
        Assert.Throws<InvalidOperationException>(() => second.StartMonitoring(Sync, new()));
        await connection.CloseAsync();
    }
}
