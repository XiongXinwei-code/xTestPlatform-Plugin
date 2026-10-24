using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class WriteLifetimeTests
{
    [Fact]
    public async Task Configured_write_deadline_returns_error_but_retains_real_native_call()
    {
        using var exit = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = exit };
        var owner = new CanConnectionResource(raw); var write = owner.WriteAsync(new() { Id = 0x123, Data = [1] }, 40);
        await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(() => write);
            Assert.Contains("40", error.Message); Assert.NotNull(owner.IsolationReason);
            Assert.Throws<InvalidOperationException>(() => owner.Write(new() { Id = 0x124, Data = [2] }));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(25));
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount);
        }
        finally { exit.Set(); await owner.CloseAsync(); }
        Assert.Equal(1, raw.CloseCount);
    }

    [Fact]
    public async Task Already_cancelled_write_never_submits_native_work()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.WriteAsync(new() { Id = 0x123, Data = [1] }, 1000, stop.Token));
        Assert.False(raw.WriteEntered.Task.IsCompleted); Assert.Empty(raw.Sent); await owner.CloseAsync();
    }

    [Fact]
    public async Task Cancelled_queued_write_cannot_submit_before_its_waiter_resumes()
    {
        using var exit = new ManualResetEventSlim(); using var stop = new CancellationTokenSource();
        var raw = new FakeAdapter { WriteBlock = exit }; var owner = new CanConnectionResource(raw);
        var first = Task.Run(() => owner.Write(new() { Id = 0x123, Data = [1] }));
        await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = owner.WriteAsync(new() { Id = 0x124, Data = [2] }, 5000, stop.Token);
        try
        {
            // Let the native-submission worker queue on the transmit gate.
            await Task.Delay(50);
            // Registered last, this callback runs before the waiter's cancellation
            // callback. Cancellation is already visible in the caller token.
            using var releaseBeforeWaiter = stop.Token.Register(() =>
            {
                exit.Set();
                SpinWait.SpinUntil(() => second.IsCompleted, TimeSpan.FromSeconds(1));
            });
            stop.Cancel();
            try { await second; } catch (OperationCanceledException) { }
            Assert.DoesNotContain(raw.Sent, frame => frame.Id == 0x124);
        }
        finally { exit.Set(); await first; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Caller_cancellation_does_not_mean_the_frame_was_withdrawn()
    {
        using var exit = new ManualResetEventSlim(); using var stop = new CancellationTokenSource();
        var raw = new FakeAdapter { WriteBlock = exit }; var owner = new CanConnectionResource(raw);
        var writing = owner.WriteAsync(new() { Id = 0x123, Data = [1] }, 1000, stop.Token);
        await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writing);
            Assert.NotNull(owner.IsolationReason); Assert.False(owner.IsClosed);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(25));
        }
        finally { exit.Set(); await owner.CloseAsync(); }
        Assert.Single(raw.Sent); // Submission can complete after the caller stopped waiting.
    }
}
