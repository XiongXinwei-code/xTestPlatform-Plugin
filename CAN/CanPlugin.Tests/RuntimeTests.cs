using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using CAN.Adapters;
using CAN.CANopen;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

public class FakeAdapter : ICanAdapter
{
    public BlockingCollection<CanMessage> Incoming { get; } = new();
    public ConcurrentQueue<CanMessage> Sent { get; } = new();
    public Action<CanMessage>? OnWrite { get; set; }
    public ManualResetEventSlim? WriteBlock { get; set; }
    public ManualResetEventSlim? ReadBlock { get; set; }
    public Exception? ReadFailure { get; set; }
    public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource WriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsConnected { get; private set; } = true;
    public int CloseCount { get; private set; }
    public void Open(CanAdapterConfig config) => IsConnected = true;
    public void Close() { CloseCount++; IsConnected = false; }
    public void Dispose() { }
    public void Write(CanMessage message)
    {
        WriteEntered.TrySetResult();
        WriteBlock?.Wait();
        OnWrite?.Invoke(message);
        Sent.Enqueue(message);
    }
    public CanMessage? Read(int timeoutMs, CancellationToken ct = default)
    {
        ReadEntered.TrySetResult();
        ReadBlock?.Wait();
        if (ReadFailure != null) throw ReadFailure;
        return Incoming.TryTake(out var frame, timeoutMs, ct) ? frame : null;
    }
    public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
}
public sealed class ConfirmedAdapter : FakeAdapter, ICanConfirmedTransmitter
{
    public void WriteConfirmed(CanMessage frame, CancellationToken ct) => Write(frame);
}

[Collection("CAN blocked I/O")]
public sealed class RuntimeTests
{
    private static FeedbackRoute[] Routes => [new("A", 0x181, 2, 100), new("B", 0x281, 2, 100), new("C", 0x381, 2, 100), new("D", 0x481, 2, 100)];
    private static OrderedFrame[] Targets => [new(0x201, [1, 2]), new(0x301, [3, 4])];
    private static OrderedFrame Sync => new(0x80, []);
    private static async Task Until(Func<bool> check)
    {
        var clock = Stopwatch.StartNew();
        while (!check())
        {
            if (clock.ElapsedMilliseconds > 3000) throw new TimeoutException("测试条件未满足");
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task FourSnapshotsDoNotConsumeFifoOrWaitForMissingRoute()
    {
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            var immediate = s.Snapshot();
            Assert.All(immediate.Receive.Frames, f => Assert.Equal("NeverReceived", f.Quality));
            foreach (var id in new uint[] { 0x281, 0x481, 0x381, 0x181 }) fake.Incoming.Add(new() { Id = id, Data = [1, 2] });
            await Until(() => s.Snapshot().Receive.Frames.All(f => f.Sequence == 1));
            foreach (var r in Routes) Assert.Equal(r.CobId, (await CanopenConnectionAccess.For(c).ReceiveAsync(r.CobId, 100, default))!.Frame.Id);
            Assert.All(s.Snapshot().Receive.Frames, f => Assert.Equal(1, f.Sequence));
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task MissingIdDoesNotBlockAnotherIdOrSdoResponse()
    {
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            c.StartService("one", Routes);
            var access = CanopenConnectionAccess.For(c);
            var missing = access.ReceiveAsync(0x181, 150, default);
            fake.Incoming.Add(new() { Id = 0x581, Data = [0x43, 0x41, 0x60, 0, 1, 2, 3, 4] });
            Assert.NotNull(await access.ReceiveAsync(0x581, 100, default));
            Assert.Null(await missing);
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task StaleHardwareFrameNeverBecomesFreshAtDequeueAndUnknownTimeIsInvalid()
    {
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            fake.Incoming.Add(new() { Id = 0x181, Data = [1, 2], TimestampNs = 10, TimestampDomain = "TestMonotonic", ClockId = "test",
                CaptureMonotonicLowerBoundTicks = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 2 });
            fake.Incoming.Add(new() { Id = 0x281, Data = [1, 2], TimestampNs = 123 });
            await Until(() => s.Snapshot().Receive.Frames[1].Sequence == 1);
            var a = s.Snapshot().Receive.Frames[0];
            Assert.Equal("Stale", a.Quality); Assert.True(a.AgeUpperBoundMs >= 2000);
            Assert.Equal("TimeUnknown", s.Snapshot().Receive.Frames[1].Quality);
            var again = s.Snapshot().Receive.Frames[0];
            Assert.Equal(a.IngressMonotonicTicks, again.IngressMonotonicTicks);
            Assert.Equal(a.Sequence, again.Sequence);
            Assert.Null(s.Snapshot().Receive.QueueStatistics.DriverOverflows);
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task WrongDlcReplacesOldValueAndFifoEvictionsAreSeparateFromLatestCoalescing()
    {
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            for (var i = 0; i < 70; i++) fake.Incoming.Add(new() { Id = 0x181, Data = [1, 2] });
            fake.Incoming.Add(new() { Id = 0x181, Data = [1] });
            await Until(() => s.Snapshot().Receive.Frames[0].Sequence == 71);
            var snapshot = s.Snapshot().Receive;
            Assert.Equal("LengthMismatch", snapshot.Frames[0].Quality);
            Assert.Equal(7, snapshot.FifoEvictions); Assert.Equal(70, snapshot.LatestReplacements);
            Assert.Equal(64, snapshot.PendingFrames);
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task SdoAndObserverShareResponseRouting()
    {
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            fake.Incoming.Add(new() { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0, 0, 0, 0, 0] });
            await Until(() => s.Snapshot().Receive.PendingFrames == 1);
            fake.OnWrite = message =>
            {
                fake.Incoming.Add(new() { Id = 0x281, Data = [1, 2] });
                fake.Incoming.Add(new() { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0, 0x37, 0x17, 0, 0] });
            };
            using var node = CanopenConnectionAccess.For(c).ReserveNode(1, 1000);
            Assert.Equal(new byte[] { 0x37, 0x17 }, await node.ReadObjectAsync(0x6041, 0, default));
            Assert.Equal(1, s.Snapshot().Receive.Frames[1].Sequence);
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task RawProtocolCannotStealReceiveAndFailedStartDoesNotSend()
    {
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            Assert.Throws<InvalidOperationException>(() => c.ClaimRawProtocol());
            Assert.Null(c.Read(1)); // Raw observation is now a separate routed copy.
            Assert.Throws<NotSupportedException>(() => s.StartTargets([1], Targets, Sync, new()));
            Assert.Empty(fake.Sent);
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task ConfirmedCyclesPreserveOrderAndStopKeepsAcquisition()
    {
        var fake = new ConfirmedAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            s.StartTargets([1], Targets, Sync, new(50, 1000, 1000, true, 5000));
            await Until(() => s.Snapshot().Cycle.CompletedCycles >= 2);
            await s.StopTargetsAsync();
            var frames = fake.Sent.ToArray();
            for (var i = 0; i + 2 < frames.Length; i += 3) Assert.Equal(new uint[] { 0x201, 0x301, 0x80 }, frames.Skip(i).Take(3).Select(f => f.Id));
            Assert.Equal("StoppedConfirmed", s.Snapshot().Cycle.State);
            var count = frames.Length;
            await Task.Delay(70); Assert.Equal(count, fake.Sent.Count);
            fake.Incoming.Add(new() { Id = 0x181, Data = [1, 2] });
            await Until(() => s.Snapshot().Receive.Frames[0].Sequence == 1);
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task RpdoFailurePreventsSyncAndCannotBeOverwrittenByRestart()
    {
        var fake = new ConfirmedAdapter(); var c = new CanConnectionResource(fake);
        var s = c.StartService("one", Routes);
        fake.OnWrite = _ => throw new InvalidOperationException("injected failure");
        s.StartTargets([1], Targets, Sync, new(50, 1000, 1000, true, 5000));
        await Until(() => s.Snapshot().Cycle.State == "Faulted");
        Assert.DoesNotContain(fake.Sent, f => f.Id == 0x80);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.StopTargetsAsync());
        Assert.Throws<InvalidOperationException>(() => s.StartTargets([1], Targets, Sync, new()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.CloseAsync());
        Assert.True(c.IsClosed);
        Assert.Equal(1, fake.CloseCount); // 发送仍未知，但实际 I/O 已退出，父连接仍执行合法 SDK 关闭。
        Assert.True(s.Snapshot().Cycle.PartialCycle);
        Assert.False(s.Snapshot().Cycle.StopCommandsAcknowledged);
    }

    [Fact]
    public async Task BlockedNativeWriteCannotCloseOrRestartUntilActualExit()
    {
        using var release = new ManualResetEventSlim();
        var fake = new ConfirmedAdapter { WriteBlock = release }; var c = new CanConnectionResource(fake);
        var s = c.StartService("one", Routes);
        s.StartTargets([1], Targets, Sync, new(1000, 5000, 1000, true, 5000));
        await fake.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => s.StopTargetsAsync(30));
            await Assert.ThrowsAsync<TimeoutException>(() => c.CloseAsync(30));
            Assert.Equal(0, fake.CloseCount);
            Assert.Throws<InvalidOperationException>(() => s.StartTargets([1], Targets, Sync, new()));
        }
        finally { release.Set(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.CloseAsync(2000));
        Assert.True(c.IsClosed);
        Assert.Equal(1, fake.CloseCount);
        Assert.DoesNotContain(fake.Sent, f => f.Id == 0x80);
    }

    [Fact]
    public async Task PublishedPayloadIsCopiedAndChangingLayoutIsRejected()
    {
        var fake = new ConfirmedAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            var input = Targets;
            s.StartTargets([1], input, Sync, new(100, 1000, 1000, true, 5000));
            input[0].Data[0] = 99;
            await Until(() => fake.Sent.Count >= 3);
            Assert.Equal(1, fake.Sent.First().Data[0]);
            Assert.Throws<ArgumentException>(() => s.PublishTargets([new(0x202, [1, 2])]));
            var next = Targets; next[0].Data[0] = 9; s.PublishTargets(next); next[0].Data[0] = 77;
            await Until(() => fake.Sent.Any(f => f.Id == 0x201 && f.Data[0] == 9));
            await s.StopTargetsAsync();
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task OldGenericJobIsConnectionScopedAndCloseWaitsForIt()
    {
        var a = new CanConnectionResource(new FakeAdapter()); var b = new CanConnectionResource(new FakeAdapter());
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        a.StartJob("same", _ => exit.Task); b.StartJob("same", token => Task.Delay(Timeout.Infinite, token));
        await b.StopJobAsync("same", 1000, default);
        await Assert.ThrowsAsync<TimeoutException>(() => a.CloseAsync(20));
        exit.SetResult(); await a.CloseAsync(2000); await b.CloseAsync();
    }

    [Fact]
    public async Task ExistingAxisLeaseCanCoexistWithCycleAndSurvivesItsStop()
    {
        var fake = new ConfirmedAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            using var axis = CanopenConnectionAccess.For(c).ReserveNode(1, 1000);
            var s = c.StartService("one", Routes);
            s.StartTargets([1], Targets, Sync, new(60000, 5000, 1000));
            await Until(() => s.Snapshot().Cycle.CompletedCycles == 1);
            await s.StopTargetsAsync();
            Assert.Throws<InvalidOperationException>(() => CanopenConnectionAccess.For(c).ReserveNode(1, 1000));
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task ModeSwitchRejectsInFlightRawReadAndProtocolClaim()
    {
        using var release = new ManualResetEventSlim();
        var fake = new FakeAdapter { ReadBlock = release }; var c = new CanConnectionResource(fake);
        var reading = Task.Run(() => c.Read(5));
        await fake.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try { Assert.Throws<TimeoutException>(() => c.StartService("one", Routes, 30)); Assert.False(c.Controlled); }
        finally { release.Set(); }
        await reading;
        c.ClaimRawProtocol();
        Assert.Throws<InvalidOperationException>(() => c.StartService("one", Routes));
        await c.CloseAsync();
    }

    [Fact]
    public async Task CancellationCallbackCannotBlockStopCallerOrAllowPrematureClose()
    {
        using var release = new ManualResetEventSlim();
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        c.StartJob("callbacks", async ct =>
        {
            using var registration = ct.Register(() => release.Wait());
            registered.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
        });
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            var task = Task.Run(() => c.StopJobAsync("callbacks", 30, default));
            await Assert.ThrowsAsync<TimeoutException>(() => task.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.False(task.IsCompletedSuccessfully);
            Assert.Equal(0, fake.CloseCount);
        }
        finally { release.Set(); }
        await c.CloseAsync();
    }

    [Fact]
    public async Task ReceiveFailureAppearsInSnapshotAndStopsTargets()
    {
        var fake = new FakeAdapter { ReadFailure = new InvalidOperationException("receive failure") };
        var c = new CanConnectionResource(fake);
        var s = c.StartService("one", Routes);
        await Until(() => s.Snapshot().Receive.BackgroundError != null);
        Assert.Contains("receive failure", s.Snapshot().Receive.BackgroundError!);
        Assert.False(s.Snapshot().Receive.AllValid);
        Assert.All(s.Snapshot().Receive.Frames, f => Assert.Equal("SourceFault", f.Quality));
        var closeError = await Assert.ThrowsAsync<InvalidOperationException>(() => c.CloseAsync());
        Assert.Contains("receive failure", closeError.ToString());
        Assert.True(c.IsClosed);
    }

    [Fact]
    public async Task InputExpiryStopsFurtherCyclesWithoutAutomaticRetry()
    {
        var fake = new ConfirmedAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            s.StartTargets([1], Targets, Sync, new(50, 1000, 1000, true, 80));
            await Until(() => s.Snapshot().Cycle.State == "Faulted");
            var count = fake.Sent.Count;
            await Task.Delay(100);
            Assert.Equal(count, fake.Sent.Count);
            Assert.Contains("输入已过期", s.Snapshot().Cycle.Error!);
            await s.StopTargetsAsync();
            Assert.Throws<InvalidOperationException>(() => s.StartTargets([1], Targets, Sync, new()));
        }
        finally { await Assert.ThrowsAsync<InvalidOperationException>(() => c.CloseAsync()); }
    }

    [Fact]
    public async Task DuplicateClockTimestampAndClockResetCannotBeValid()
    {
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        try
        {
            var s = c.StartService("one", Routes);
            CanMessage Frame(string clock) => new() { Id = 0x181, Data = [1, 2], TimestampNs = 100, TimestampDomain = "Test",
                ClockId = clock, CaptureMonotonicLowerBoundTicks = Stopwatch.GetTimestamp() };
            fake.Incoming.Add(Frame("A")); fake.Incoming.Add(Frame("A"));
            await Until(() => s.Snapshot().Receive.Frames[0].Sequence == 2);
            Assert.Equal("OutOfOrder", s.Snapshot().Receive.Frames[0].Quality);
            fake.Incoming.Add(Frame("B"));
            await Until(() => s.Snapshot().Receive.Frames[0].Sequence == 3);
            Assert.Equal("ClockReset", s.Snapshot().Receive.Frames[0].Quality);
        }
        finally { await c.CloseAsync(); }
    }

    [Fact]
    public async Task SamePhysicalChannelCannotBeReopenedUnderDifferentLogicalName()
    {
        var key = Guid.NewGuid().ToString();
        var c = new CanConnectionResource(new FakeAdapter(), key);
        Assert.Throws<InvalidOperationException>(() => new CanConnectionResource(new FakeAdapter(), key));
        await c.CloseAsync();
        var next = new CanConnectionResource(new FakeAdapter(), key); await next.CloseAsync();
    }

    [Theory]
    [InlineData(CanAdapterType.Kvaser, "0", "+00")]
    [InlineData(CanAdapterType.Vector, "1", "01")]
    [InlineData(CanAdapterType.TOSUN, "0", "00")]
    [InlineData(CanAdapterType.PEAK, "PCAN_USBBUS1", "01")]
    [InlineData(CanAdapterType.ZLG, "USBCAN2/0/1", "ZCAN_USBCAN_II/00/01")]
    public void PhysicalAliasesHaveOneIdentity(CanAdapterType type, string first, string second)
        => Assert.Equal(CanChannelIdentity.Normalize(type, first), CanChannelIdentity.Normalize(type, second));

    [Fact]
    public async Task MidBatchNativeFailureMustRetainSyncOwnershipForLatchedTargets()
    {
        using var release = new ManualResetEventSlim();
        var fake = new ConfirmedAdapter { WriteBlock = release, OnWrite = frame =>
            { if (frame.Id == 0x301) throw new IOException("fake second member failure"); } };
        var c = new CanConnectionResource(fake); var s = c.StartService("one", Routes);
        s.StartTargets([1], Targets, Sync, new(1000, 5000, 1000, true, 5000));
        await fake.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stop = s.StopTargetsAsync();
        release.Set();
        await Assert.ThrowsAsync<InvalidOperationException>(() => stop);
        Assert.Throws<InvalidOperationException>(() => c.Write(new CanMessage { Id = 0x80 }));
        Assert.Throws<InvalidOperationException>(() => s.StartTargets([1], Targets, Sync, new()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.CloseAsync());
        Assert.True(c.IsClosed); // 已释放句柄，但仍向调用方报告部分周期故障。
    }

    [Fact]
    public async Task CloseMustReportGenericBackgroundFailureEvenWithoutStopStep()
    {
        var fake = new FakeAdapter(); var c = new CanConnectionResource(fake);
        var job = c.StartJob("failed", _ => Task.FromException(new IOException("generic failure")));
        await job.Completion;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => c.CloseAsync());
        Assert.Contains("generic failure", error.ToString());
        Assert.Equal(1, fake.CloseCount); // 实际工作已退出，先安全释放再报告后台故障。
    }

    [Fact]
    public async Task LateNativeFailureMustNotBeHiddenByStopCancellation()
    {
        using var release = new ManualResetEventSlim();
        var fake = new ConfirmedAdapter { WriteBlock = release, OnWrite = _ => throw new IOException("late native failure") };
        var c = new CanConnectionResource(fake); var s = c.StartService("one", Routes);
        s.StartTargets([1], Targets, Sync, new(1000, 5000, 1000, true, 5000));
        await fake.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stop = s.StopTargetsAsync();
        release.Set();
        await Assert.ThrowsAsync<InvalidOperationException>(() => stop);
        Assert.Contains("late native failure", s.Snapshot().Cycle.Error!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.CloseAsync());
    }
}
