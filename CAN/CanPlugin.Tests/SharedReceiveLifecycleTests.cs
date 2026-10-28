using System.Reflection;
using CAN.CANopen;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class SharedReceiveLifecycleTests
{
    [Fact]
    public async Task Cancellation_of_an_infinite_raw_observer_does_not_cancel_or_isolate_other_readers()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            owner.StartService("observer", []);
            using var cancelled = new CancellationTokenSource();
            var waiting = owner.ReadAsync(null, Timeout.Infinite, cancelled.Token);
            cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.Null(owner.IsolationReason);
            raw.Incoming.Add(new() { Id = 0x181, Data = [1] });
            Assert.Equal(1, (await owner.ReadCanopenAsync(0x181, 2000, default))!.Data[0]);
            Assert.Equal(1, (await owner.ReadAsync(0x181, 2000))!.Data[0]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Close_wakes_all_raw_and_axis_waiters_and_the_next_connection_can_use_the_channel()
    {
        string key = Guid.NewGuid().ToString(); var owner = new CanConnectionResource(new FakeAdapter(), key);
        using var node = CanopenConnectionAccess.For(owner).ReserveNode(1, 5000);
        var rawWait = owner.ReadAsync(null, Timeout.Infinite);
        var nodeWait = node.ReceivePdoAsync(0x181, default);
        await owner.CloseAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => rawWait);
        await Assert.ThrowsAnyAsync<Exception>(() => nodeWait);
        var next = new CanConnectionResource(new FakeAdapter(), key); await next.CloseAsync();
    }

    [Fact]
    public async Task Isolation_cannot_be_bypassed_by_cached_raw_or_axis_inbox_data()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            using var node = CanopenConnectionAccess.For(owner).ReserveNode(1, 2000);
            var service = owner.StartService("observer", [new("feedback", 0x181, 1, 1000)]);
            raw.Incoming.Add(new() { Id = 0x181, Data = [7] });
            using var guard = new CancellationTokenSource(5000);
            while (service.Snapshot().Receive.Frames[0].Sequence == 0) await Task.Delay(2, guard.Token);
            owner.Isolate("fixture unresolved native transaction");
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ReadAsync(0x181, 2000));
            await Assert.ThrowsAsync<InvalidOperationException>(() => node.ReceivePdoAsync(0x181, default));
        }
        finally { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task An_inbox_lock_wait_cannot_consume_a_frame_after_the_configured_deadline()
    {
        var inbox = new CanFrameInbox(); inbox.Publish(new() { Id = 0x181, Data = [7] });
        var gate = typeof(CanFrameInbox).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(inbox)!;
        using var held = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var holding = Task.Run(() => { lock (gate) { held.Set(); release.Wait(5000); } });
        Assert.True(held.Wait(5000));
        var reading = Task.Run(() => inbox.ReadAsync(0x181, 30, default));
        try { await Task.Delay(100); }
        finally { release.Set(); await holding; }
        Assert.Null(await reading);
        Assert.Equal(7, (await inbox.ReadAsync(0x181, 0, default))!.Data[0]);
        inbox.Dispose();
    }

    [Fact]
    public async Task Raw_observation_backlog_is_bounded_and_does_not_fill_or_consume_the_protocol_fifo()
    {
        var router = new CanReceiveRouter();
        for (int i = 0; i < CanFrameInbox.Capacity + 2; i++) router.Publish(new() { Id = 0x181, Data = [1] });
        Assert.Equal(64, router.Diagnostics.Depth);
        Assert.Contains("RawInboxDepth=4096", router.FanoutDiagnostics);
        Assert.Contains("RawInboxEvictions=2", router.FanoutDiagnostics);
        await router.ReadRawAsync(0x181, 1000, default, null);
        Assert.Equal(64, router.Diagnostics.Depth);
        router.Finish(null);
    }

    [Fact]
    public async Task Conversion_wait_and_Pdo_receive_use_one_configured_deadline()
    {
        using var release = new ManualResetEventSlim();
        var raw = new FakeAdapter { ReadBlock = release }; var owner = new CanConnectionResource(raw);
        var old = Task.Run(() => owner.Read(1));
        await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var receiving = CanopenConnectionAccess.For(owner).ReceiveAsync(0x181, 500, default);
        try
        {
            await Task.Delay(300); release.Set(); await old;
            await Task.Delay(350); raw.Incoming.Add(new() { Id = 0x181, Data = [9] });
            Assert.Null(await receiving);
        }
        finally { release.Set(); await old; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Service_start_returns_a_Task_while_waiting_for_the_old_native_reader_and_cancel_does_not_start_a_pump()
    {
        using var release = new ManualResetEventSlim(); using var caller = new CancellationTokenSource();
        var raw = new FakeAdapter { ReadBlock = release }; var owner = new CanConnectionResource(raw);
        var old = Task.Run(() => owner.Read(1));
        await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var starting = owner.StartServiceAsync("observer", [], 5000, caller.Token, () => { });
            Assert.False(starting.IsCompleted);
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
            Assert.False(owner.Controlled); Assert.Null(owner.IsolationReason); Assert.Equal(0, raw.CloseCount);
        }
        finally { release.Set(); await old; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task First_shared_mode_waits_for_an_inflight_raw_Sdo_submission_within_the_same_budget()
    {
        using var release = new ManualResetEventSlim();
        var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        var writing = owner.WriteAsync(new() { Id = 0x601, Data = [1] }, 5000);
        await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var starting = owner.StartServiceAsync("observer", [], 5000, default, () => { });
        try
        {
            Assert.NotSame(starting, await Task.WhenAny(starting, Task.Delay(100)));
            release.Set(); await writing; await starting;
            Assert.True(owner.Controlled); Assert.Null(owner.IsolationReason);
        }
        finally
        {
            release.Set(); await writing;
            try { await starting; } catch (Exception) { }
            await owner.CloseAsync();
        }
    }

    [Fact]
    public async Task Cancellation_before_legacy_confirmed_adapter_entry_does_not_invent_unknown_native_submission()
    {
        var raw = new ConfirmedAdapter(); var owner = new CanConnectionResource(raw);
        var service = owner.StartService("diagnostic", []);
        var tick = (SemaphoreSlim)typeof(CanopenCyclicService).GetField("_tickGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        await tick.WaitAsync();
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(60000, 5000, 1000));
            var token = ((CancellationTokenSource)typeof(CanopenCyclicService).GetField("_stop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!).Token;
            var progress = new List<CanBatchProgressKind>();
            Assert.ThrowsAny<OperationCanceledException>(() => owner.SendBatch(service, [new(0x80, [0])], true, token, value =>
            {
                progress.Add(value.Kind); if (value.Kind == CanBatchProgressKind.NativeAdmission) service.RequestStop();
            }, CanConfirmationBudget.Start(5000, token)));
            Assert.Equal(new[] { CanBatchProgressKind.NativeAdmission, CanBatchProgressKind.NativeNotEntered }, progress);
            Assert.Empty(raw.Sent); Assert.Null(owner.IsolationReason);
        }
        finally { tick.Release(); try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task First_member_completed_and_second_member_proven_not_entered_is_still_a_partial_batch()
    {
        var raw = new ConfirmedAdapter(); var owner = new CanConnectionResource(raw);
        var service = owner.StartService("diagnostic", []);
        var tick = (SemaphoreSlim)typeof(CanopenCyclicService).GetField("_tickGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(60000, 5000, 1000));
        using var guard = new CancellationTokenSource(5000);
        while (service.Snapshot().Cycle.CompletedCycles == 0) await Task.Delay(2, guard.Token);
        await tick.WaitAsync(); int before = raw.Sent.Count;
        try
        {
            var token = ((CancellationTokenSource)typeof(CanopenCyclicService).GetField("_stop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!).Token;
            var progress = new CanopenCyclicService.BatchProgress(service, 2, true); int admissions = 0;
            Assert.ThrowsAny<OperationCanceledException>(() => owner.SendBatch(service, [new(0x201, [1]), new(0x80, [0])], true, token,
                value =>
                {
                    progress.Report(value);
                    if (value.Kind == CanBatchProgressKind.NativeAdmission && ++admissions == 2) service.RequestStop();
                }, CanConfirmationBudget.Start(5000, token), purpose: CanBatchPurpose.Target));
            Assert.Equal(new uint[] { 0x201 }, raw.Sent.Skip(before).Select(frame => frame.Id));
            Assert.Equal(before + 1, service.Snapshot().Cycle.SubmittedFrames);
            Assert.Equal(before + 1, service.Snapshot().Cycle.SubmissionCompletedFrames);
            Assert.True(service.Snapshot().Cycle.PartialCycle);
            Assert.NotNull(owner.IsolationReason);
        }
        finally { tick.Release(); try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }
}
