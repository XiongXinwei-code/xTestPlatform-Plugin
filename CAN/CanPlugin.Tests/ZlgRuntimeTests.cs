using System.Collections.Concurrent;
using CAN.Adapters.Zlg;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.ContractTests;

[Collection("CAN blocked I/O")]
public class ZlgRuntimeTests
{
    [Fact]
    public void HistoricalReceiveTimeoutIsReportedAfterActualHandleRelease()
    {
        var driver = new Driver { ReadFailure = new TimeoutException("driver receive failure") };
        var adapter = new ZlgAdapter(driver, 41, false);
        Assert.True(SpinWait.SpinUntil(() => !adapter.ConfirmationAvailable, 2000));
        var error = Assert.Throws<InvalidOperationException>(adapter.Close);
        Assert.Contains("driver receive failure", error.ToString()); Assert.True(adapter.HandlesReleased);
    }
    [Fact]
    public async Task ConfirmedZlgRunsMonitoringAndOwnerStopOnSharedCanopenRouter()
    {
        var driver = new Driver(); var adapter = new ZlgAdapter(driver, 41, true);
        var connection = new CanConnectionResource(adapter);
        driver.OnClassicTransmit = data =>
        {
            if (data.frame.can_id == 0x601)
                driver.Classic.Enqueue(new() { timestamp = 250, frame = new() { can_id = 0x581, can_dlc = 8,
                    data = [0x60, data.frame.data[1], data.frame.data[2], data.frame.data[3], 0, 0, 0, 0] } });
        };
        var service = connection.StartService("zlg", [new("arbitrary-status", 0x181, 1, 100)]);
        service.ConfigureControlledStop(new([new(1, 0x2000, 0, [0])], 500));
        service.StartMonitoring(new(0x80, [0]), new(40, 1000, 2000));
        service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(100, 1000, 2000));
        await Task.Delay(120); await service.StopTargetsAsync();
        Assert.Equal("Monitoring", service.Snapshot().Cycle.State);
        Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
        Assert.False(service.Snapshot().Cycle.HostExited);
        Assert.Throws<InvalidOperationException>(() => connection.Write(new() { Id = 0x201, Data = [2] }));
        await connection.CloseAsync(); Assert.True(connection.IsClosed);
    }
    [Fact]
    public void ReopenResetsTimestampBarrierAndConnectionEpoch()
    {
        var driver = new Driver(); var adapter = new ZlgAdapter(driver, 41, false);
        adapter.WriteConfirmed(new() { Id = 0x201, Data = [1] }, default);
        adapter.Close(); driver.ResetClock();
        adapter.Open(new() { Channel = "USBCANFD-200U/0/0", Protocol = CanProtocolType.FD });
        using var timeout = new CancellationTokenSource(1000);
        adapter.WriteConfirmed(new() { Id = 0x201, Data = [1] }, timeout.Token);
        Assert.True(adapter.ConfirmTransmitQuiescent()); adapter.Close();
    }
    [Fact]
    public void FdThenClassicEchoCanArriveFromSeparateQueuesInEitherOrder()
    {
        var driver = new Driver { BlockReceive = true }; var adapter = new ZlgAdapter(driver, 41, true);
        Assert.True(driver.Receiving.Wait(2000));
        adapter.Write(new() { Id = 0x201, Data = [1], IsFd = true });
        adapter.Write(new() { Id = 0x202, Data = [2] });
        driver.ReleaseReceive.Set();
        try { Assert.True(SpinWait.SpinUntil(adapter.ConfirmTransmitQuiescent, 2000)); }
        finally
        {
            // Retry both actual echoes for cleanup on a regressed implementation.
            driver.Fd.Enqueue(driver.LastFd); driver.Classic.Enqueue(driver.LastClassic);
            adapter.Close();
        }
    }

    [Fact]
    public async Task SubmissionWaitsForEchoAndNativeExit()
    {
        var driver = new Driver { BlockTransmit = true, AutoEcho = true };
        var adapter = new ZlgAdapter(driver, 41, true);
        var writing = Task.Run(() => adapter.WriteConfirmed(new() { Id = 0x201, Data = [1] }, default));
        Assert.True(driver.Transmitting.Wait(2000));
        await Task.Delay(25); Assert.False(writing.IsCompleted);
        driver.ReleaseTransmit.Set();
        await writing.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(adapter.ConfirmTransmitQuiescent());
        adapter.Close(); Assert.Equal(1, driver.Closed);
    }

    [Fact]
    public async Task DuplicateEchoCannotConfirmNextIdenticalPayloadAndCancellationKeepsPending()
    {
        var driver = new Driver(); var adapter = new ZlgAdapter(driver, 41, false);
        adapter.WriteConfirmed(new() { Id = 0x201, Data = [1] }, default);
        var first = driver.LastClassic;
        driver.AutoEcho = false;
        using var cts = new CancellationTokenSource();
        var next = Task.Run(() => adapter.WriteConfirmed(new() { Id = 0x201, Data = [1] }, cts.Token));
        Assert.True(SpinWait.SpinUntil(() => driver.Writes == 2, 2000));
        driver.Classic.Enqueue(first); // same timestamp as already consumed echo
        await Task.Delay(25); Assert.False(next.IsCompleted);
        cts.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
        Assert.False(adapter.ConfirmTransmitQuiescent());
        Assert.Throws<InvalidOperationException>(() => adapter.Write(new() { Id = 0x201, Data = [2] }));
        Assert.Throws<TimeoutException>(adapter.Close); Assert.Equal(0, driver.Closed);
        driver.EmitLast();
        Assert.True(SpinWait.SpinUntil(adapter.ConfirmTransmitQuiescent, 2000));
        adapter.Close(); Assert.Equal(1, driver.Closed);
    }

    [Fact]
    public void UnsupportedModelKeepsOrdinaryWriteButRejectsStrictModeBeforeSending()
    {
        var driver = new Driver(); var adapter = new ZlgAdapter(driver, 4, false);
        Assert.False(adapter.ConfirmationAvailable);
        Assert.Throws<NotSupportedException>(adapter.VerifyConfirmationAvailable);
        Assert.Equal(0, driver.Writes);
        adapter.Write(new() { Id = 0x601, Data = [1] });
        Assert.Equal(1, driver.Writes); adapter.Close();
    }

    [Fact]
    public void FdConnectionDrainsClassicAndFdQueuesAndDoesNotExposeEchoAsFeedback()
    {
        var driver = new Driver(); var adapter = new ZlgAdapter(driver, 41, true);
        adapter.WriteConfirmed(new() { Id = 0x80, Data = [0] }, default);
        driver.Classic.Enqueue(new() { timestamp = 200, frame = new() { can_id = 0x181, can_dlc = 1, data = [7] } });
        driver.Fd.Enqueue(new() { timestamp = 210, frame = new() { can_id = 0x281, len = 1, data = [8] } });
        var frames = new[] { adapter.Read(1000)!, adapter.Read(1000)! };
        Assert.Contains(frames, f => f.Id == 0x181 && !f.IsFd);
        Assert.Contains(frames, f => f.Id == 0x281 && f.IsFd);
        Assert.DoesNotContain(frames, f => f.IsTransmitEcho);
        Assert.All(frames, f => Assert.NotNull(f.RawTimestampUs));
        adapter.Close();
    }

    [Fact]
    public async Task PumpMustExitBeforeResetAndCloseAndCloseFailureRetainsHandle()
    {
        var driver = new Driver { BlockReceive = true }; var adapter = new ZlgAdapter(driver, 41, false);
        Assert.True(driver.Receiving.Wait(2000));
        Assert.Throws<TimeoutException>(adapter.Close);
        Assert.Equal(0, driver.Reset); Assert.Equal(0, driver.Closed);
        driver.ReleaseReceive.Set(); await Task.Delay(30);
        driver.FailClose = true;
        Assert.Throws<InvalidOperationException>(adapter.Close); Assert.False(adapter.HandlesReleased);
        driver.FailClose = false; adapter.Close(); Assert.True(adapter.HandlesReleased);
    }

    internal sealed class Driver : ZlgIo
    {
        public ConcurrentQueue<ZlgApi.ZCAN_Receive_Data> Classic { get; } = new();
        public ConcurrentQueue<ZlgApi.ZCAN_ReceiveFD_Data> Fd { get; } = new();
        public ManualResetEventSlim Transmitting { get; } = new();
        public ManualResetEventSlim ReleaseTransmit { get; } = new();
        public ManualResetEventSlim Receiving { get; } = new();
        public ManualResetEventSlim ReleaseReceive { get; } = new();
        public bool BlockTransmit, BlockReceive, FailClose;
        public Exception? ReadFailure;
        public volatile bool AutoEcho = true;
        public int Writes, Reset, Closed;
        public ZlgApi.ZCAN_Receive_Data LastClassic;
        public ZlgApi.ZCAN_ReceiveFD_Data LastFd;
        public Action<ZlgApi.ZCAN_Transmit_Data>? OnClassicTransmit;
        private long _time = 100;
        public void ResetClock() { _time = 0; LastClassic = default; LastFd = default; }
        public override IntPtr OpenDevice(uint type, uint index) => new(1);
        public override IntPtr InitCan(IntPtr device, uint channel, ref ZlgApi.ZCAN_CHANNEL_INIT_CONFIG config) => new(2);
        public override uint StartCan(IntPtr channel) => 1;
        public override uint ClearBuffer(IntPtr channel)
        {
            while (Classic.TryDequeue(out _)) { }
            while (Fd.TryDequeue(out _)) { }
            return 1;
        }
        public override bool SetProperty(IntPtr device, string path, string value) => true;
        public override uint Transmit(IntPtr channel, ref ZlgApi.ZCAN_Transmit_Data data)
        {
            LastClassic = new() { frame = data.frame, timestamp = (ulong)Interlocked.Increment(ref _time) };
            Interlocked.Increment(ref Writes); if (AutoEcho) Classic.Enqueue(LastClassic);
            OnClassicTransmit?.Invoke(data);
            Transmitting.Set(); if (BlockTransmit) ReleaseTransmit.Wait(); return 1;
        }
        public override uint TransmitFd(IntPtr channel, ref ZlgApi.ZCAN_TransmitFD_Data data)
        {
            LastFd = new() { frame = data.frame, timestamp = (ulong)Interlocked.Increment(ref _time) };
            Interlocked.Increment(ref Writes); if (AutoEcho) Fd.Enqueue(LastFd);
            Transmitting.Set(); if (BlockTransmit) ReleaseTransmit.Wait(); return 1;
        }
        public void EmitLast()
        {
            if (LastFd.frame.data != null) Fd.Enqueue(LastFd);
            else Classic.Enqueue(LastClassic);
        }
        public override uint Receive(IntPtr channel, ZlgApi.ZCAN_Receive_Data[] output, int waitMs)
        {
            Receiving.Set(); if (BlockReceive) ReleaseReceive.Wait();
            if (ReadFailure != null) throw ReadFailure;
            var count = 0; while (count < output.Length && Classic.TryDequeue(out var item)) output[count++] = item;
            return (uint)count;
        }
        public override uint ReceiveFd(IntPtr channel, ZlgApi.ZCAN_ReceiveFD_Data[] output, int waitMs)
        {
            var count = 0; while (count < output.Length && Fd.TryDequeue(out var item)) output[count++] = item;
            return (uint)count;
        }
        public override uint ResetCan(IntPtr channel) { Reset++; return 1; }
        public override uint CloseDevice(IntPtr device) { if (FailClose) return 0; Closed++; return 1; }
    }
}
