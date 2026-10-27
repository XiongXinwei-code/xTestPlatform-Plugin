using System.Diagnostics;
using CAN.CANopen;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class MixedCanopenUseTests
{
    private static FakeAdapter Driver()
    {
        var raw = new FakeAdapter();
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x601 && frame.Data.Length == 8)
                raw.Incoming.Add(new() { Id = 0x581, Data = frame.Data[0] == 0x40
                    ? [0x4B, frame.Data[1], frame.Data[2], frame.Data[3], 0x27, 0, 0, 0]
                    : [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
        };
        return raw;
    }
    private static async Task Until(Func<bool> condition)
    { using var guard = new CancellationTokenSource(5000); while (!condition()) await Task.Delay(2, guard.Token); }
    private static async Task Close(CanConnectionResource owner)
    { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }

    [Fact]
    public async Task Raw_Can_read_and_typed_Sdo_both_receive_the_response_without_stealing_it()
    {
        var owner = new CanConnectionResource(Driver());
        try
        {
            owner.StartService("observer", []);
            var observing = owner.ReadAsync(0x581, 2000);
            var data = await new CanopenSdoClient(new CanopenBus(owner), 1, 2000).UploadAsync(0x6041, 0, false, default);
            Assert.Equal(new byte[] { 0x27, 0 }, data);
            Assert.Equal(new byte[] { 0x4B, 0x41, 0x60, 0, 0x27, 0, 0, 0 }, (await observing)!.Data);
            Assert.Null(owner.IsolationReason);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Ordinary_Sdo_read_and_write_can_supplement_a_bound_axis_without_releasing_its_lease()
    {
        var owner = new CanConnectionResource(Driver());
        try
        {
            using var axis = CanopenConnectionAccess.For(owner).ReserveNode(1, 2000);
            var client = new CanopenSdoClient(new CanopenBus(owner), 1, 2000);
            Assert.Equal(new byte[] { 0x27, 0 }, await client.UploadAsync(0x6041, 0, false, default));
            await client.DownloadAsync(0x2000, 0, [1], false, default);
            Assert.Throws<InvalidOperationException>(() => CanopenConnectionAccess.For(owner).ReserveNode(1, 2000));
            Assert.Equal(new byte[] { 0x27, 0 }, await axis.ReadObjectAsync(0x6041, 0, default));
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Axis_cycle_and_manual_Pdo_Sync_and_generic_job_can_share_one_connection()
    {
        var raw = Driver(); var owner = new CanConnectionResource(raw);
        try
        {
            using var axis = CanopenConnectionAccess.For(owner).ReserveNode(1, 2000);
            var service = owner.StartService("cycle", []);
            service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(60000, 5000, 1000, false, 60000));
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            await owner.WriteAsync(new() { Id = 0x201, Data = [9] }, 2000);
            await new CanopenBus(owner).SendAsync(0x80, [0], 2000, default);
            owner.StartJob("supplement", async ct => await owner.WriteAsync(new() { Id = 0x202, Data = [3] }, 2000, ct));
            await owner.StopJobAsync("supplement", 5000, default);
            await service.StopTargetsAsync(); await owner.StopServiceAsync("cycle", 5000, default);
            Assert.Equal(new byte[] { 0x27, 0 }, await axis.ReadObjectAsync(0x6041, 0, default));
            Assert.Contains(raw.Sent, frame => frame.Id == 0x201 && frame.Data[0] == 9);
            Assert.True(owner.IsConnected);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Shared_raw_reads_keep_extended_Fd_and_original_capture_time()
    {
        var raw = Driver(); var owner = new CanConnectionResource(raw);
        try
        {
            owner.StartService("observer", []);
            long captured = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            raw.Incoming.Add(new() { Id = 0x1ABCDE, FrameType = CanFrameType.Extended, IsFd = true, Data = new byte[12],
                CaptureMonotonicLowerBoundTicks = captured, TimestampNs = 42, TimestampDomain = "fixture", ClockId = "A" });
            var frame = await owner.ReadAsync(0x1ABCDE, 2000);
            Assert.True(frame!.IsFd); Assert.Equal(CanFrameType.Extended, frame.FrameType);
            Assert.Equal(captured, frame.CaptureMonotonicLowerBoundTicks); Assert.Equal(42, frame.TimestampNs);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Axis_Pdo_receive_and_Canopen_receive_and_raw_observer_have_independent_copies()
    {
        var raw = Driver(); var owner = new CanConnectionResource(raw);
        try
        {
            using var axis = CanopenConnectionAccess.For(owner).ReserveNode(1, 2000);
            owner.StartService("observer", []);
            raw.Incoming.Add(new() { Id = 0x181, Data = [7] });
            var axisFrame = axis.ReceivePdoAsync(0x181, default);
            var canopenFrame = new CanopenBus(owner).ReceiveAsync(0x181, 2000, default);
            var rawFrame = owner.ReadAsync(0x181, 2000);
            Assert.Equal(7, (await axisFrame)!.Frame.Data[0]);
            Assert.Equal(7, (await canopenFrame)!.Data[0]); Assert.Equal(7, (await rawFrame)!.Data[0]);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Raw_Sdo_submission_cannot_enter_an_active_typed_Sdo_transaction()
    {
        var raw = Driver(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        raw.OnWrite = frame => { if (frame.Id == 0x601 && frame.Data[0] == 0x40) entered.TrySetResult(); };
        var owner = new CanConnectionResource(raw);
        Task<byte[]>? reading = null; Task? supplemental = null;
        try
        {
            owner.StartService("observer", []);
            reading = new CanopenSdoClient(new CanopenBus(owner), 1, 2000).UploadAsync(0x6041, 0, false, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            supplemental = owner.WriteAsync(new() { Id = 0x601, Data = [0x2F, 0, 0x20, 0, 1, 0, 0, 0] }, 2000);
            Assert.NotSame(supplemental, await Task.WhenAny(supplemental, Task.Delay(100)));
            raw.Incoming.Add(new() { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0, 0x27, 0, 0, 0] });
            await reading; await supplemental; Assert.Equal(2, raw.Sent.Count);
        }
        finally
        {
            raw.Incoming.Add(new() { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0, 0x27, 0, 0, 0] });
            if (reading != null) try { await reading; } catch (Exception) { }
            if (supplemental != null) try { await supplemental; } catch (Exception) { }
            await Close(owner);
        }
    }

    [Fact]
    public async Task A_whole_batch_waiting_for_the_Sdo_gate_is_withdrawn_by_motion_stop_without_submitting_any_member()
    {
        var raw = Driver(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        raw.OnWrite = frame => { if (frame.Id == 0x601 && frame.Data[0] == 0x40) entered.TrySetResult(); };
        var owner = new CanConnectionResource(raw);
        Task<byte[]>? reading = null;
        try
        {
            var service = owner.StartService("cycle", []);
            reading = new CanopenSdoClient(new CanopenBus(owner), 1, 5000).UploadAsync(0x6041, 0, false, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            service.StartTargets([2], [new(0x601, [1])], new(0x80, [0]), new(60000, 5000, 1000, false, 60000));
            await Until(() => !service.Snapshot().Cycle.OwnedSendTaskExited);
            await service.StopTargetsAsync(2000);
            Assert.Single(raw.Sent); Assert.Equal(0, service.Snapshot().Cycle.SubmittedFrames);
            Assert.Null(owner.IsolationReason);
            raw.Incoming.Add(new() { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0, 0x27, 0, 0, 0] }); await reading;
        }
        finally
        {
            raw.Incoming.Add(new() { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0, 0x27, 0, 0, 0] });
            if (reading != null) try { await reading; } catch (Exception) { }
            await Close(owner);
        }
    }
}
