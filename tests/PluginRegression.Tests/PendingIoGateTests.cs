extern alias EthernetCore;
using System.IO;
using PendingIoGate = EthernetCore::PluginCommon.PendingIoGate;
using Xunit;

namespace PluginRegression.Tests;

public sealed class PendingIoGateTests
{
    [Fact]
    public async Task Cancelled_caller_cannot_get_success_when_another_cancel_callback_delays_wait_notification()
    {
        var gate = new PendingIoGate();
        var entered = NewSignal(); var exit = NewSignal();
        using var caller = new CancellationTokenSource();
        using var callbackExit = new ManualResetEventSlim();
        var callbackEntered = NewSignal();
        var running = gate.RunAsync(async () => { entered.TrySetResult(); await exit.Task; return 42; }, 5000, "读取", caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var registration = caller.Token.Register(() => { callbackEntered.TrySetResult(); callbackExit.Wait(10000); });
        var cancelling = Task.Run(caller.Cancel);
        try
        {
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            exit.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            Assert.NotNull(gate.IsolatedReason);
        }
        finally { callbackExit.Set(); exit.TrySetResult(); await cancelling; await gate.CloseAsync(() => { }, 1000, default); }
    }

    [Fact]
    public async Task Cancelled_wait_keeps_native_call_and_blocks_following_io_until_confirmed_close()
    {
        var gate = new PendingIoGate();
        var started = NewSignal();
        var exit = NewSignal();
        using var caller = new CancellationTokenSource();
        var waiting = gate.RunAsync(async () => { started.SetResult(); await exit.Task; return 1; }, 2000, "读", caller.Token);
        await started.Task;
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.True(gate.HasPendingOperation);
        Assert.NotNull(gate.IsolatedReason);
        bool submitted = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync(() => { submitted = true; return Task.FromResult(2); }, 200, "后续", default));
        Assert.False(submitted);
        bool released = false;
        await Assert.ThrowsAsync<TimeoutException>(() => gate.CloseAsync(() => released = true, 30, default));
        Assert.False(released);
        Assert.False(gate.IsClosed);
        exit.SetResult();
        await gate.CloseAsync(() => released = true, 2000, default);
        Assert.True(released);
        Assert.True(gate.IsClosed);
    }

    [Fact]
    public async Task Already_cancelled_caller_never_submits_operation()
    {
        var gate = new PendingIoGate();
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        bool submitted = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.RunBlockingAsync(() => { submitted = true; return 1; }, 200, "写", caller.Token));
        Assert.False(submitted);
        Assert.False(gate.HasPendingOperation);
    }

    [Fact]
    public async Task Timeout_retains_operation_and_close_executes_exactly_once()
    {
        var gate = new PendingIoGate();
        var exit = NewSignal();
        var entered = NewSignal();
        var running = gate.RunAsync(async () => { entered.TrySetResult(); await exit.Task; return 1; }, 1000, "读", default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); // Prove actual submission; queue expiry is covered separately.
        await Assert.ThrowsAsync<TimeoutException>(() => running);
        Assert.True(gate.HasPendingOperation);
        int closes = 0;
        var close1 = gate.CloseAsync(() => Interlocked.Increment(ref closes), 2000, default);
        var close2 = gate.CloseAsync(() => Interlocked.Increment(ref closes), 2000, default);
        exit.SetResult();
        await Task.WhenAll(close1, close2);
        Assert.Equal(1, closes);
    }

    [Fact]
    public async Task Failed_close_is_reported_and_can_be_retried()
    {
        var gate = new PendingIoGate();
        await Assert.ThrowsAsync<IOException>(() => gate.CloseAsync(() => throw new IOException("close failed"), 2000, default));
        Assert.False(gate.IsClosed);
        await gate.CloseAsync(() => { }, 2000, default);
        Assert.True(gate.IsClosed);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Worker_delayed_until_after_deadline_and_close_never_submits()
    {
        var gate = new PendingIoGate();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int barrierUsed = 0, submitted = 0;
        var barrier = new AsyncLocal<bool>(change =>
        {
            if (change.ThreadContextChanged && change.CurrentValue &&
                Interlocked.Exchange(ref barrierUsed, 1) == 0)
            {
                entered.Set();
                release.Wait();
            }
        });
        barrier.Value = true;
        Task waiting = gate.RunBlockingAsync(() => Interlocked.Increment(ref submitted), 50, "延迟写入", default);
        barrier.Value = false;
        try
        {
            Assert.True(await Task.Run(() => entered.Wait(2000)));
            await Assert.ThrowsAsync<TimeoutException>(() => waiting);
            var closing = gate.CloseAsync(() => { }, 2000, default);
            release.Set();
            await closing;
            Assert.Equal(0, submitted);
            Assert.True(gate.IsClosed);
        }
        finally { release.Set(); await gate.CloseAsync(() => { }, 2000, default); }
    }

    [Fact]
    public async Task Explicit_infinite_wait_still_cancels_without_losing_the_pending_operation()
    {
        var gate = new PendingIoGate();
        var started = NewSignal();
        var exit = NewSignal();
        using var caller = new CancellationTokenSource();
        var waiting = gate.RunAsync(async () => { started.SetResult(); await exit.Task; return 1; }, -1, "无限读取", caller.Token);
        try
        {
            var winner = await Task.WhenAny(started.Task, waiting);
            Assert.Same(started.Task, winner);
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.True(gate.HasPendingOperation);
        }
        finally { exit.TrySetResult(); await gate.CloseAsync(() => { }, 2000, default); }
    }
}
