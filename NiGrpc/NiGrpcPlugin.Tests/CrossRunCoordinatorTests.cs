using NiGrpc.Runtime;
using Xunit;

namespace NiGrpc.Tests;

public sealed class CrossRunCoordinatorTests
{
    [Fact]
    public async Task Close_timeout_before_clear_keeps_owner_and_finishes_after_known_io_exit()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = await gate.EnterAsync(generation, stopped.Task, 1000, default);
        var wire = new PendingWire();
        var owner = new RemoteConnection(wire, wire, 50, track: lease.TrackCleanup, unknown: lease.MarkUnknown);
        lease.RegisterConnection(() => owner.RequestClose(50), owner.CloseCompletion, owner.CloseStarted);
        var session = await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        var invoke = session.InvokeAsync("WriteAnalogF64", "{}", 3000, default);
        await Assert.ThrowsAnyAsync<TimeoutException>(() => owner.CloseAsync(40, default));
        Assert.False(owner.LocalChannelClosed);
        Assert.Equal(0, wire.Clears);
        wire.Pending.SetResult("{}"); await invoke;
        await owner.RequestClose(50).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(owner.LocalChannelClosed);
        Assert.Equal(1, wire.Clears);
        lease.Dispose(); stopped.SetResult();
        using var next = await gate.EnterAsync(new object(), Task.Delay(-1), 2000, default);
    }
    [Fact]
    public async Task Session_close_holds_normal_admission_until_actual_cleanup_exits()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        using var lease = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        var finishing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closing = lease.BeginSessionClose();
        lease.TrackCleanup(finishing.Task);
        closing.Dispose();
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(generation, Task.Delay(-1), 40, default));
        using var cleanup = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default, cleanup: true);
        finishing.SetResult();
        using var next = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
    }
    [Fact]
    public async Task Cancelling_one_step_does_not_retire_unrelated_connections()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        using var caller = new CancellationTokenSource();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var lease = await gate.EnterAsync(generation, stopped.Task, 1000, caller.Token);
        lease.RegisterConnection(() => { closed.TrySetResult(); return Task.CompletedTask; });
        caller.Cancel();
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(generation, stopped.Task, 40, default));
        Assert.False(closed.Task.IsCompleted);
        lease.Dispose();
        using var next = await gate.EnterAsync(generation, stopped.Task, 1000, default);
        Assert.False(closed.Task.IsCompleted);
        next.Dispose(); stopped.SetResult();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
    [Fact]
    public async Task Cancelled_step_pending_work_blocks_same_run_until_actual_exit()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        using var caller = new CancellationTokenSource();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = await gate.EnterAsync(generation, Task.Delay(-1), 1000, caller.Token);
        lease.Track(pending.Task); caller.Cancel(); lease.Dispose();
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(generation, Task.Delay(-1), 40, default));
        pending.SetResult();
        using var next = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
    }
    [Fact]
    public async Task Existing_lease_rechecks_close_barrier_before_submission()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        using var first = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        using var second = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        using (first.BeginSessionClose())
        {
            Assert.Throws<InvalidOperationException>(() => second.CheckAdmission(false));
            second.CheckAdmission(true);
        }
        second.CheckAdmission(false);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.RegisterConnection(() => Task.CompletedTask, closeStarted: started.Task);
        started.SetResult();
        Assert.Throws<InvalidOperationException>(() => second.CheckAdmission(false));
    }
    [Fact]
    public async Task Clear_confirmation_is_not_published_before_local_finish_is_tracked()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        using var lease = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        using var caller = new CancellationTokenSource();
        using var callbackEntered = new ManualResetEventSlim(); using var callbackRelease = new ManualResetEventSlim();
        using var trackEntered = new ManualResetEventSlim(); using var trackRelease = new ManualResetEventSlim();
        bool armed = false;
        var wire = new PendingWire { OnClear = caller.Cancel, ClearCallback = () => { callbackEntered.Set(); callbackRelease.Wait(); } };
        var owner = new RemoteConnection(wire, wire, 50, track: task => { if (armed) { trackEntered.Set(); trackRelease.Wait(); } lease.TrackCleanup(task); }, unknown: lease.MarkUnknown);
        lease.RegisterConnection(() => owner.RequestClose(50), owner.CloseCompletion, owner.CloseStarted);
        var session = await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        armed = true;
        try
        {
            var close = Task.Run(() => session.CloseAsync(1000, caller.Token));
            Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(2)));
            Assert.True(trackEntered.Wait(TimeSpan.FromSeconds(2)));
            var ownerClose = owner.RequestClose(50);
            await Assert.ThrowsAnyAsync<TimeoutException>(() => ownerClose.WaitAsync(TimeSpan.FromMilliseconds(120)));
            await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(generation, Task.Delay(-1), 40, default));
            trackRelease.Set(); callbackRelease.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => close);
            await ownerClose.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally { trackRelease.Set(); callbackRelease.Set(); }
    }
    [Fact]
    public async Task Waiting_new_generation_exits_when_its_scope_is_stopped()
    {
        var gate = new CrossRunCoordinator();
        using var old = await gate.EnterAsync(new object(), Task.Delay(-1), 1000, default);
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = gate.EnterAsync(new object(), stop.Task, 5000, default);
        stop.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(1)));
    }
    [Fact]
    public async Task Same_run_waits_for_confirmed_close_local_callback_exit()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        var lease = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        using var caller = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var wire = new PendingWire { OnClear = caller.Cancel, ClearCallback = () => { entered.Set(); release.Wait(); } };
        var owner = new RemoteConnection(wire, wire, 50, track: lease.TrackCleanup, unknown: lease.MarkUnknown);
        lease.RegisterConnection(() => owner.RequestClose(50), owner.CloseCompletion, owner.CloseStarted);
        var session = await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        Task? ownerClose = null;
        try
        {
            var explicitClose = session.CloseAsync(1000, caller.Token);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => explicitClose);
            ownerClose = owner.RequestClose(50);
            await Assert.ThrowsAnyAsync<TimeoutException>(() => ownerClose.WaitAsync(TimeSpan.FromMilliseconds(120)));
            await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(generation, Task.Delay(-1), 40, default));
            using var cleanup = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default, cleanup: true);
        }
        finally { release.Set(); }
        await ownerClose!.WaitAsync(TimeSpan.FromSeconds(2));
        using var next = await gate.EnterAsync(generation, Task.Delay(-1), 2000, default);
        lease.Dispose();
    }
    [Fact]
    public async Task Same_run_new_operation_waits_while_existing_connection_close_is_pending()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        using var lease = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        var wire = new PendingWire();
        var owner = new RemoteConnection(wire, wire, 1000, track: lease.TrackCleanup, unknown: lease.MarkUnknown);
        lease.RegisterConnection(() => owner.RequestClose(1000), owner.CloseCompletion, owner.CloseStarted);
        var session = await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        var invoke = session.InvokeAsync("WriteAnalogF64", "{}", 1000, default);
        var close = owner.RequestClose(1000);
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(generation, Task.Delay(-1), 40, default));
        wire.Pending.SetResult("{}"); await invoke; await close;
        using var next = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
    }
    [Fact]
    public async Task Late_old_close_notification_does_not_release_or_poison_new_generation()
    {
        var gate = new CrossRunCoordinator();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = await gate.EnterAsync(new object(), stop.Task, 1000, default);
        old.RegisterConnection(() => Task.CompletedTask, late.Task);
        old.Dispose(); stop.SetResult();
        var newGeneration = new object();
        using var next = await gate.EnterAsync(newGeneration, Task.Delay(-1), 1000, default);
        var newClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        next.RegisterConnection(() => newClose.Task, newClose.Task);
        late.SetResult();
        await Task.Delay(30);
        Assert.Equal(1, gate.ConnectionCount);
        using var sameGeneration = await gate.EnterAsync(newGeneration, Task.Delay(-1), 1000, default);
        newClose.SetResult();
    }

    [Fact]
    public async Task Repeated_successful_connections_do_not_accumulate_registrations()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        using var lease = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        for (int i = 0; i < 100; i++)
        {
            var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lease.RegisterConnection(() => complete.Task, complete.Task);
            complete.SetResult();
        }
        for (int i = 0; i < 100 && gate.ConnectionCount != 0; i++) await Task.Delay(5);
        Assert.Equal(0, gate.ConnectionCount);
    }
    [Fact]
    public async Task Closed_connection_registration_is_removed_and_old_callback_cannot_remove_new_one()
    {
        var gate = new CrossRunCoordinator();
        var lease = await gate.EnterAsync(new object(), Task.Delay(-1), 1000, default);
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lease.RegisterConnection(() => first.Task, first.Task);
        lease.RegisterConnection(() => second.Task, second.Task);
        first.SetResult();
        for (int i = 0; i < 100 && gate.ConnectionCount != 1; i++) await Task.Delay(5);
        Assert.Equal(1, gate.ConnectionCount);
        lease.Dispose();
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(new object(), Task.Delay(-1), 40, default));
        second.SetResult();
        using var next = await gate.EnterAsync(new object(), Task.Delay(-1), 1000, default);
        Assert.Equal(0, gate.ConnectionCount);
    }
    [Fact]
    public async Task Closing_waits_for_known_pending_io_and_blocks_other_connections()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        var lease = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        var wire = new PendingWire();
        var owner = new RemoteConnection(wire, wire, 50, track: lease.TrackCleanup, unknown: lease.MarkUnknown);
        lease.RegisterConnection(() => owner.RequestClose(50), owner.CloseCompletion, owner.CloseStarted);
        var session = await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        var invoke = session.InvokeAsync("WriteAnalogF64", "{}", 1000, default);
        var closing = owner.RequestClose(40);
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(generation, Task.Delay(-1), 40, default));
        using var close = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default, cleanup: true);
        wire.Pending.SetResult("{}"); await invoke;
        await closing.WaitAsync(TimeSpan.FromSeconds(2));
        using var next = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        lease.Dispose();
    }

    [Fact]
    public async Task Confirmed_clear_with_pending_local_callback_releases_after_real_exit()
    {
        var gate = new CrossRunCoordinator();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = await gate.EnterAsync(new object(), stop.Task, 1000, default);
        using var caller = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var wire = new PendingWire { OnClear = caller.Cancel, ClearCallback = () => { entered.Set(); release.Wait(); } };
        var owner = new RemoteConnection(wire, wire, 50, track: lease.TrackCleanup, unknown: lease.MarkUnknown);
        lease.RegisterConnection(() => owner.RequestClose(50));
        var session = await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        try
        {
            var explicitClose = session.CloseAsync(1000, caller.Token);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => explicitClose);
            lease.Dispose(); stop.SetResult();
            await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(new object(), Task.Delay(-1), 120, default));
        }
        finally { release.Set(); }
        using var next = await gate.EnterAsync(new object(), Task.Delay(-1), 2000, default);
        Assert.Equal(1, wire.Clears);
    }
    [Fact]
    public async Task Known_open_completed_during_close_can_release_after_clear()
    {
        var gate = new CrossRunCoordinator();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = await gate.EnterAsync(new object(), stop.Task, 1000, default);
        var wire = new PendingWire { PendingOpen = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var owner = new RemoteConnection(wire, wire, 1000, track: lease.TrackCleanup, unknown: lease.MarkUnknown);
        lease.RegisterConnection(() => owner.RequestClose(1000));
        var opening = owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        var close = owner.RequestClose(1000);
        wire.PendingOpen.SetResult(new OpenReply(wire.OpenName!, true, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => opening);
        await close;
        Assert.Equal(1, wire.Clears);
        lease.Dispose(); stop.SetResult();
        using var next = await gate.EnterAsync(new object(), Task.Delay(-1), 1000, default);
    }
    [Fact]
    public async Task Timed_out_remote_write_keeps_cross_run_quarantine_after_client_task_finishes()
    {
        var gate = new CrossRunCoordinator();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = await gate.EnterAsync(new object(), stop.Task, 1000, default);
        var wire = new PendingWire();
        var connection = new RemoteConnection(wire, wire, 100, track: lease.TrackCleanup, unknown: lease.MarkUnknown);
        lease.RegisterConnection(() => connection.RequestClose(100));
        var session = await connection.OpenSessionAsync("one", NiDriver.Daqmx, "{}", 1000, default);
        await Assert.ThrowsAsync<TimeoutException>(() => session.InvokeAsync("WriteAnalogF64", "{}", 40, default));
        wire.Pending.SetResult("{}");
        lease.Dispose(); stop.SetResult();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => gate.EnterAsync(new object(), Task.Delay(-1), 1000, default));
        Assert.Contains("隔离", error.Message);
        Assert.Equal(0, wire.Clears);
    }

    [Fact]
    public async Task Scope_retirement_requests_close_without_blocking_caller()
    {
        var gate = new CrossRunCoordinator();
        using var caller = new CancellationTokenSource();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = await gate.EnterAsync(new object(), stopped.Task, 1000, caller.Token);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lease.RegisterConnection(() => { closed.TrySetResult(); return Task.CompletedTask; });
        stopped.SetResult();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        lease.Dispose();
        using var next = await gate.EnterAsync(new object(), Task.Delay(-1), 1000, default);
    }

    [Fact]
    public async Task Cleanup_can_enter_same_generation_even_while_quarantined()
    {
        var gate = new CrossRunCoordinator(); var generation = new object();
        using var owner = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default);
        owner.MarkUnknown("结果未知");
        using var close = await gate.EnterAsync(generation, Task.Delay(-1), 1000, default, cleanup: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.EnterAsync(generation, Task.Delay(-1), 1000, default));
    }
    [Fact]
    public async Task New_run_waits_for_old_step_and_connection_cleanup()
    {
        var gate = new CrossRunCoordinator();
        var oldStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldLease = await gate.EnterAsync(new object(), oldStop.Task, 1000, default);
        oldLease.RegisterConnection(() => close.Task);
        oldStop.SetResult();
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(new object(), newStop.Task, 40, default));
        oldLease.Dispose();
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(new object(), newStop.Task, 40, default));
        close.SetResult();
        using var next = await gate.EnterAsync(new object(), newStop.Task, 1000, default);
    }

    [Fact]
    public async Task Unknown_old_rpc_stays_isolated_after_local_task_and_close_exit()
    {
        var gate = new CrossRunCoordinator();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = await gate.EnterAsync(new object(), stop.Task, 1000, default);
        old.MarkUnknown("远端写入结果未知");
        old.RegisterConnection(() => Task.CompletedTask);
        old.Dispose(); stop.SetResult();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => gate.EnterAsync(new object(), Task.Delay(-1), 1000, default));
        Assert.Contains("隔离", error.Message);
    }

    [Fact]
    public async Task Completed_outer_step_does_not_release_pending_real_work()
    {
        var gate = new CrossRunCoordinator();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actual = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = await gate.EnterAsync(new object(), stop.Task, 1000, default);
        old.Track(actual.Task); old.Dispose(); stop.SetResult();
        await Assert.ThrowsAsync<TimeoutException>(() => gate.EnterAsync(new object(), Task.Delay(-1), 40, default));
        actual.SetResult();
        using var next = await gate.EnterAsync(new object(), Task.Delay(-1), 1000, default);
    }

    private sealed class PendingWire : IRemoteWire, IDisposable
    {
        internal readonly TaskCompletionSource<string> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<OpenReply>? PendingOpen;
        internal string? OpenName;
        internal Action? OnClear, ClearCallback;
        internal int Clears;
        public Task<OpenReply> OpenAsync(NiDriver driver, string name, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null) { OpenName = name; return PendingOpen?.Task ?? Task.FromResult(new OpenReply(name, true, 0)); }
        public Task<string> InvokeAsync(NiDriver driver, string name, string method, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null) => Pending.Task;
        public Task ClearAsync(NiDriver driver, string name, int timeoutMs, CancellationToken ct) { Clears++; if (ClearCallback != null) ct.Register(ClearCallback); OnClear?.Invoke(); return Task.CompletedTask; }
        public void Dispose() { }
    }
}
