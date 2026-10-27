extern alias EthernetCore;
using PendingIoGate = EthernetCore::PluginCommon.PendingIoGate;
using Xunit;

namespace PluginRegression.Tests;

public sealed class AsyncReleaseGateTests
{
    [Fact]
    public async Task Async_release_waits_for_real_io_then_keeps_the_gate_until_actual_release_finishes()
    {
        var gate = new PendingIoGate();
        var ioEntered = Signal(); var nativeIo = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEntered = Signal(); var nativeRelease = Signal();
        var operation = gate.RunAsync(async () => { ioEntered.TrySetResult(); return await nativeIo.Task; }, 10000, "read", default);
        await ioEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        gate.RequestAsyncClose(async () => { releaseEntered.TrySetResult(); await nativeRelease.Task; });
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
            Assert.False(releaseEntered.Task.IsCompleted);
            nativeIo.TrySetResult(1);
            await releaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(gate.IsClosed); Assert.False(gate.CloseCompletion!.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync(() => Task.FromResult(2), 100, "new read", default));
            nativeRelease.TrySetResult();
            await gate.CloseCompletion!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(gate.IsClosed);
        }
        finally { nativeIo.TrySetResult(1); nativeRelease.TrySetResult(); await gate.CloseCompletion!.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public async Task Async_release_timeout_ends_only_the_wait_and_failed_completed_release_can_be_retried()
    {
        var gate = new PendingIoGate();
        var entered = Signal(); var release = Signal(); int calls = 0;
        Task FirstRelease()
        {
            Interlocked.Increment(ref calls); entered.TrySetResult();
            return FailAfterRelease();
        }
        async Task FailAfterRelease() { await release.Task; throw new InvalidOperationException("fake async release failure"); }
        var timed = gate.CloseWithReleaseAsync(FirstRelease, 30, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => timed);
            Assert.False(gate.IsClosed); Assert.Equal(1, calls);
            release.TrySetResult();
            await Assert.ThrowsAsync<InvalidOperationException>(() => gate.CloseCompletion!);
            Assert.False(gate.IsClosed);
            await gate.CloseWithReleaseAsync(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; }, 1000, default);
            Assert.True(gate.IsClosed); Assert.Equal(2, calls);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task Concurrent_close_waiters_share_one_actual_async_release()
    {
        var gate = new PendingIoGate();
        var entered = Signal(); var release = Signal(); int calls = 0, secondCalls = 0;
        var first = gate.CloseWithReleaseAsync(async () => { Interlocked.Increment(ref calls); entered.TrySetResult(); await release.Task; }, 5000, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var actual = gate.CloseCompletion;
        var second = gate.CloseWithReleaseAsync(() => { Interlocked.Increment(ref secondCalls); return Task.CompletedTask; }, 5000, default);
        try
        {
            Assert.Same(actual, gate.CloseCompletion);
            Assert.Equal(1, calls); Assert.Equal(0, secondCalls);
            Assert.False(first.IsCompleted); Assert.False(second.IsCompleted); Assert.False(gate.IsClosed);
            release.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(gate.IsClosed); Assert.Equal(1, calls); Assert.Equal(0, secondCalls);
        }
        finally { release.TrySetResult(); await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inline_scheduling_handles_completed_and_asynchronously_failed_release_without_losing_the_gate(bool fail)
    {
        var gate = new PendingIoGate(work => work());
        var release = Signal(); int calls = 0;
        var closing = gate.CloseWithReleaseAsync(() => { Interlocked.Increment(ref calls); return fail ? release.Task : Task.CompletedTask; }, 5000, default);
        if (fail)
        {
            Assert.False(closing.IsCompleted); Assert.False(gate.IsClosed);
            release.SetException(new InvalidOperationException("fake asynchronous release failure"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => closing);
            Assert.False(gate.IsClosed);
            await gate.CloseWithReleaseAsync(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; }, 1000, default);
            Assert.Equal(2, calls);
        }
        else
        {
            await closing;
            Assert.Equal(1, calls);
        }
        Assert.True(gate.IsClosed);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
