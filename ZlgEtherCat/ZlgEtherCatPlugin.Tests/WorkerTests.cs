using ZlgEtherCat.Runtime;
using Xunit;

namespace ZlgEtherCatPlugin.Tests;

public sealed class WorkerTests
{
    [Fact]
    public async Task TimeoutBetweenDeadlineCheckAndAdmissionWithdrawsWorkAtomically()
    {
        using var atAdmission = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var worker = new SdkWorker("admission-race", () => { atAdmission.Set(); release.Wait(); });
        int calls = 0;
        var request = worker.InvokeAsync(() => ++calls, 80, CancellationToken.None);
        Assert.True(atAdmission.Wait(2000));
        await Assert.ThrowsAsync<TimeoutException>(() => request);
        release.Set();
        await worker.InvokeAsync(() => 1, 1000, CancellationToken.None);
        await worker.CloseAsync(() => { });
        Assert.Equal(0, calls);
        Assert.Null(worker.IsolatedReason);
    }

    [Fact]
    public async Task TimeoutRetainsActualCallAndCloseWaitsForExit()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var worker = new SdkWorker("test");
        var pending = worker.InvokeAsync(() => { entered.Set(); release.Wait(); return 1; }, 100, CancellationToken.None);
        Assert.True(entered.Wait(2000));
        await Assert.ThrowsAsync<TimeoutException>(() => pending);
        Assert.NotNull(worker.IsolatedReason);
        bool closed = false;
        var close = worker.CloseAsync(() => closed = true);
        Assert.False(close.IsCompleted);
        Assert.False(closed);
        release.Set();
        await close.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(closed);
    }

    [Fact]
    public async Task CancelledQueuedWorkNeverCallsSdk()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var worker = new SdkWorker("queue-test");
        var first = worker.InvokeAsync(() => { entered.Set(); release.Wait(); return 1; }, 3000, CancellationToken.None);
        Assert.True(entered.Wait(2000));
        using var cancel = new CancellationTokenSource();
        int calls = 0;
        var second = worker.InvokeAsync(() => ++calls, 3000, cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        release.Set();
        await first;
        await worker.CloseAsync(() => { });
        Assert.Equal(0, calls);
        Assert.Equal(1, worker.WorkerStarts);
    }
}
