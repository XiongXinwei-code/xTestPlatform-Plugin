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
    public async Task Missing_echo_does_not_block_legal_device_close_or_become_a_false_confirmation()
    {
        var driver = new Driver { AutoEcho = false }; var adapter = new ZlgAdapter(driver, 41, false);
        using var caller = new CancellationTokenSource();
        var writing = Task.Run(() => adapter.WriteConfirmed(new() { Id = 0x201, Data = [3] }, caller.Token));
        Assert.True(driver.Transmitting.Wait(2000));
        caller.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writing);
        try
        {
            var error = Assert.Throws<InvalidOperationException>(adapter.Close);
            Assert.Contains("未确认", error.ToString());
            Assert.True(adapter.HandlesReleased); Assert.Equal(1, driver.Closed);
            Assert.False(adapter.ConfirmTransmitQuiescent());
            Assert.Contains("关闭时未确认发送=1", adapter.GetReceiveDiagnostics());
            driver.AutoEcho = true;
            adapter.Open(new() { Channel = "USBCANFD-200U/0/0" });
            adapter.WriteConfirmed(new() { Id = 0x201, Data = [3] }, default);
            Assert.True(adapter.ConfirmTransmitQuiescent());
            Assert.Contains("关闭时未确认发送=0", adapter.GetReceiveDiagnostics());
        }
        finally
        {
            // A regressed close that still retains the ticket can finish using its
            // real echo; this is cleanup only, never the acceptance workaround.
            driver.EmitLast();
            if (!adapter.HandlesReleased) adapter.Close();
        }
    }

    [Fact]
    public async Task Failed_device_close_keeps_unknown_ticket_and_handles_until_real_retry()
    {
        var driver = new Driver { AutoEcho = false, FailClose = true }; var adapter = new ZlgAdapter(driver, 41, false);
        using var caller = new CancellationTokenSource();
        var writing = Task.Run(() => adapter.WriteConfirmed(new() { Id = 0x201, Data = [4] }, caller.Token));
        Assert.True(driver.Transmitting.Wait(2000)); caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writing);
        try
        {
            var error = Assert.ThrowsAny<Exception>(adapter.Close);
            Assert.Contains("CloseDevice", error.ToString());
            Assert.False(adapter.HandlesReleased); Assert.False(adapter.ConfirmTransmitQuiescent());
            Assert.Contains("未决发送=1", adapter.GetReceiveDiagnostics());
            driver.FailClose = false;
            var historical = Assert.Throws<InvalidOperationException>(adapter.Close);
            Assert.Contains("未确认", historical.ToString());
            Assert.True(adapter.HandlesReleased); Assert.False(adapter.ConfirmTransmitQuiescent());
        }
        finally
        {
            driver.FailClose = false; driver.EmitLast();
            if (!adapter.HandlesReleased) adapter.Close();
        }
    }

    [Fact]
    public async Task Reset_failure_does_not_skip_final_device_release_after_real_pump_exit()
    {
        var driver = new Driver { FailReset = true }; var adapter = new ZlgAdapter(driver, 41, false);
        string key = Guid.NewGuid().ToString(); var owner = new CanConnectionResource(adapter, key);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync());
            Assert.Contains("ResetCAN", error.ToString());
            Assert.Equal(1, driver.Closed); Assert.True(adapter.HandlesReleased); Assert.True(owner.IsClosed);
            await CanConnectionResource.WaitForPreviousOwnerAsync(key, 1000);
        }
        finally
        {
            driver.FailReset = false;
            if (!adapter.HandlesReleased) await owner.CloseAsync();
        }
    }

    [Fact]
    public void Reset_and_final_close_failures_are_both_preserved_until_successful_retry()
    {
        var driver = new Driver { FailReset = true, FailClose = true }; var adapter = new ZlgAdapter(driver, 41, false);
        try
        {
            var error = Assert.ThrowsAny<Exception>(adapter.Close);
            Assert.Contains("ResetCAN", error.ToString()); Assert.Contains("CloseDevice", error.ToString());
            Assert.False(adapter.HandlesReleased); Assert.Equal(0, driver.Closed);
        }
        finally { driver.FailReset = driver.FailClose = false; adapter.Close(); }
        Assert.True(adapter.HandlesReleased); Assert.Equal(1, driver.Closed);
    }

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
        connection.Write(new() { Id = 0x201, Data = [2] });
        Assert.True(SpinWait.SpinUntil(adapter.ConfirmTransmitQuiescent, 2000)); // Strict diagnostic fixture only.
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
        var owner = new CanConnectionResource(adapter);
        var closing = owner.CloseAsync(5000);
        try
        {
            var pumpStop = (CancellationTokenSource)typeof(ZlgAdapter).GetField("_receiveStop",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(adapter)!;
            using var guard = new CancellationTokenSource(5000);
            while (!pumpStop.IsCancellationRequested) await Task.Delay(2, guard.Token);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            await Task.Delay(1300);
            Assert.False(closing.IsCompleted);
            Assert.Equal(0, driver.Reset); Assert.Equal(0, driver.Closed);
            driver.FailClose = true; driver.ReleaseReceive.Set();
            await Assert.ThrowsAsync<InvalidOperationException>(() => closing); Assert.False(adapter.HandlesReleased);
            driver.FailClose = false; await owner.CloseAsync(5000); Assert.True(adapter.HandlesReleased);
        }
        finally { driver.ReleaseReceive.Set(); driver.FailClose = false; try { await owner.CloseAsync(5000); } catch (Exception) when (owner.IsClosed) { } }
    }

    internal sealed class Driver : ZlgIo
    {
        public ConcurrentQueue<ZlgApi.ZCAN_Receive_Data> Classic { get; } = new();
        public ConcurrentQueue<ZlgApi.ZCAN_ReceiveFD_Data> Fd { get; } = new();
        public ManualResetEventSlim Transmitting { get; } = new();
        public ManualResetEventSlim ReleaseTransmit { get; } = new();
        public ManualResetEventSlim Receiving { get; } = new();
        public ManualResetEventSlim ReleaseReceive { get; } = new();
        public bool BlockTransmit, BlockReceive, FailClose, FailReset;
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
        public override uint ResetCan(IntPtr channel) { Reset++; return FailReset ? 0u : 1u; }
        public override uint CloseDevice(IntPtr device) { if (FailClose) return 0; Closed++; return 1; }
    }
}
