using ZlgEtherCat.Runtime;
using Xunit;

namespace ZlgEtherCatPlugin.Tests;

public sealed class ReadPoolTests
{
    [Fact]
    public async Task AddingAndStoppingItemsNeverCreatesAdditionalWorkers()
    {
        using var pool = new ReadFunctionPool(false);
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        for (int i = 0; i < 100; i++) pool.Add("item" + i, 10, delivery => seen.TrySetResult());
        pool.Publish(new InputSnapshot(1, System.Diagnostics.Stopwatch.GetTimestamp(), [7], 0, true));
        await seen.Task.WaitAsync(TimeSpan.FromSeconds(2));
        for (int i = 0; i < 100; i++) await pool.RemoveAsync("item" + i, CancellationToken.None);
        Assert.Equal(1, pool.WorkerStarts);
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public async Task StopWaitsForEnteredPublicationAndForbidsLatePublication()
    {
        using var pool = new ReadFunctionPool(false);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int count = 0;
        pool.Add("x", 10, _ => { entered.Set(); release.Wait(); Interlocked.Increment(ref count); });
        pool.Publish(new InputSnapshot(1, System.Diagnostics.Stopwatch.GetTimestamp(), [1], 0, true));
        Assert.True(entered.Wait(2000));
        var stopped = pool.RemoveAsync("x", CancellationToken.None);
        Assert.False(stopped.IsCompleted);
        release.Set();
        await stopped.WaitAsync(TimeSpan.FromSeconds(2));
        pool.Publish(new InputSnapshot(2, System.Diagnostics.Stopwatch.GetTimestamp(), [2], 0, true));
        await Task.Delay(40);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PoolsAreIndependentAndPeriodDoesNotCreateAWorkerPerItem()
    {
        using var background = new ReadFunctionPool(false);
        using var periodic = new ReadFunctionPool(true);
        int a = 0, b = 0;
        background.Add("same", 10, _ => Interlocked.Increment(ref a));
        periodic.Add("same", 20, _ => Interlocked.Increment(ref b));
        var frame = new InputSnapshot(1, System.Diagnostics.Stopwatch.GetTimestamp(), [1], 0, true);
        background.Publish(frame); periodic.Publish(frame);
        await Task.Delay(100);
        await background.RemoveAsync("same", CancellationToken.None);
        await periodic.RemoveAsync("same", CancellationToken.None);
        Assert.Equal(1, a);
        Assert.True(b >= 1);
        Assert.Equal(1, periodic.WorkerStarts);
    }

    [Fact]
    public async Task ConsumerFailureIsVisibleAndDoesNotKillOtherItems()
    {
        using var pool = new ReadFunctionPool(false);
        var good = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pool.Add("bad", 10, _ => throw new InvalidOperationException("发布失败"));
        pool.Add("good", 10, _ => good.TrySetResult());
        pool.Publish(new InputSnapshot(1, System.Diagnostics.Stopwatch.GetTimestamp(), [1], 0, true));
        await good.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains("发布失败", pool.Status().Single(x => x.Name == "bad").Error);
    }
}
