extern alias EthernetCore;
using RetiringResources = EthernetCore::PluginCommon.RetiringResources;
using Xunit;

namespace PluginRegression.Tests;

public sealed class RetiringResourceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Released_owner_historical_error_does_not_fail_first_reopen(bool timeoutError)
    {
        string key = Guid.NewGuid().ToString();
        bool released = false;
        RetiringResources.Track(key, new object(), (_, _) =>
        {
            released = true;
            return Task.FromException(timeoutError ? new TimeoutException("历史接收超时") :
                new InvalidOperationException("历史后台错误"));
        }, () => released);
        await RetiringResources.WaitForPreviousAsync(key, 1000);
        Assert.True(released);
        await RetiringResources.WaitForPreviousAsync(key, 1000);
    }

    [Fact]
    public async Task New_run_waits_for_actual_retirement_not_just_cancelled_waiter()
    {
        string key = Guid.NewGuid().ToString(); var owner = new object();
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool released = false;
        RetiringResources.Track(key, owner, async (_, ct) => { await exit.Task.WaitAsync(ct); released = true; }, () => released);
        await Assert.ThrowsAsync<TimeoutException>(() => RetiringResources.WaitForPreviousAsync(key, 25));
        Assert.False(released);
        var reopen = RetiringResources.WaitForPreviousAsync(key, 1000);
        Assert.False(reopen.IsCompleted); exit.SetResult(); await reopen;
        Assert.True(released);
    }

    [Fact]
    public async Task Failed_cleanup_remains_reachable_for_next_run_retry()
    {
        string key = Guid.NewGuid().ToString(); int attempts = 0; bool released = false;
        RetiringResources.Track(key, new object(), (_, _) =>
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new InvalidOperationException("native clear failed");
            released = true; return Task.CompletedTask;
        }, () => released, () => "Close/Clear phase");
        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => RetiringResources.WaitForPreviousAsync(key, 1000));
        Assert.Contains("native clear failed", first.ToString());
        Assert.Contains("Close/Clear phase", first.ToString());
        await RetiringResources.WaitForPreviousAsync(key, 1000);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Repeated_tracking_does_not_remove_a_different_retiring_owner()
    {
        string key = Guid.NewGuid().ToString(); var old = new object(); bool released = false;
        RetiringResources.Track(key, old, (_, _) => Task.CompletedTask, () => true);
        RetiringResources.Track(key, new object(), (_, _) => { released = true; return Task.CompletedTask; }, () => released);
        RetiringResources.Track(key, old, (_, _) => Task.CompletedTask, () => true);
        await RetiringResources.WaitForPreviousAsync(key, 1000);
        Assert.True(released);
    }

    [Fact]
    public async Task Cancelling_reopen_does_not_cancel_old_cleanup_or_release_its_handles()
    {
        string key = Guid.NewGuid().ToString(); bool released = false;
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RetiringResources.Track(key, new object(), async (_, ct) => { await exit.Task.WaitAsync(ct); released = true; }, () => released);
        using var cancel = new CancellationTokenSource(25);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RetiringResources.WaitForPreviousAsync(key, 1000, cancel.Token));
        Assert.False(released); exit.SetResult(); await RetiringResources.WaitForPreviousAsync(key, 1000);
        Assert.True(released);
    }
}
