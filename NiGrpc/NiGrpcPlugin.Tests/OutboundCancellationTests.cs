using NiGrpc.Runtime;
using Xunit;

namespace NiGrpc.Tests;

public sealed class OutboundCancellationTests
{
    [Fact]
    public async Task First_cancel_callback_is_tracked_when_cancel_is_repeated()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var relay = new OutboundCancellation(default);
        relay.Token.Register(() => { entered.Set(); release.Wait(); });
        relay.RequestCancel();
        relay.RequestCancel();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
        var finish = relay.FinishAsync(Task.CompletedTask);
        Assert.False(finish.IsCompleted);
        release.Set();
        await finish.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Already_cancelled_caller_is_forwarded_at_registration()
    {
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        var relay = new OutboundCancellation(caller.Token);
        Assert.True(relay.Token.IsCancellationRequested);
        await relay.FinishAsync(null);
    }

    [Fact]
    public async Task Scope_stop_is_forwarded_without_canceling_caller()
    {
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var relay = new OutboundCancellation(default, stop.Task);
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        relay.Token.Register(() => notified.TrySetResult());
        stop.SetResult();
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(relay.Token.IsCancellationRequested);
        await relay.FinishAsync(null);
    }

    [Fact]
    public async Task Throwing_transport_callback_does_not_prevent_cleanup()
    {
        var relay = new OutboundCancellation(default);
        relay.Token.Register(() => throw new InvalidOperationException("callback failure"));
        relay.RequestCancel();
        await relay.FinishAsync(Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Completion_and_cancellation_race_finishes_without_disposed_source_callback()
    {
        for (int i = 0; i < 100; i++)
        {
            using var caller = new CancellationTokenSource();
            var relay = new OutboundCancellation(caller.Token);
            var finish = Task.Run(() => relay.FinishAsync(Task.CompletedTask));
            var cancel = Task.Run(caller.Cancel);
            await Task.WhenAll(finish, cancel).WaitAsync(TimeSpan.FromSeconds(2));
        }
    }
}
