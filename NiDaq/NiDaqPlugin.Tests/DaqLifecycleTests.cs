using NiDaq.Helpers;
using Xunit;
namespace NiDaq.ContractTests;
public class DaqLifecycleTests
{
    [Fact]
    public async Task AbortDoesNotAllowDisposalBeforeNativeReadReallyExits()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var rawExit = new ManualResetEventSlim();
        bool released = false;
        var resource = new DaqTaskResource(() => aborted.TrySetResult(), () => released = true);
        await resource.ConfigureAsync(() => { }, default);
        using var cts = new CancellationTokenSource();
        var read = resource.RunAsync(() => { entered.TrySetResult(); rawExit.Wait(); return 1; }, -1, "read", cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        var closing = resource.CloseAsync(1000, default);
        await aborted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(released);
        rawExit.Set(); await closing;
        Assert.True(released);
    }
    [Fact]
    public async Task AbortFailureKeepsOwnershipAndCanBeRetried()
    {
        bool fail = true, released = false;
        var resource = new DaqTaskResource(() => { if (fail) throw new InvalidOperationException("fake abort"); }, () => released = true);
        await resource.ConfigureAsync(() => { }, default);
        await Assert.ThrowsAnyAsync<Exception>(() => resource.CloseAsync(1000, default));
        Assert.False(released);
        fail = false; await resource.CloseAsync(1000, default);
        Assert.True(released);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("timeout")]
    [InlineData("close")]
    public async Task LateNativeExitCannotSubmitNextAction(string stop)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var rawExit = new ManualResetEventSlim();
        using var cts = new CancellationTokenSource();
        bool nextSubmitted = false, released = false;
        var resource = new DaqTaskResource(() => { }, () => released = true);
        await resource.ConfigureAsync(() => { }, default);
        var request = resource.RunAsync(() =>
        {
            try
            {
                entered.TrySetResult(); rawExit.Wait();
                resource.EnsureOperationActive();
                nextSubmitted = true;
                return true;
            }
            finally { exited.TrySetResult(); }
        }, stop == "timeout" ? 100 : -1, "composite native operation", cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Task? closing = null;
        if (stop == "cancel")
        {
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        }
        else if (stop == "timeout") await Assert.ThrowsAsync<TimeoutException>(() => request);
        else closing = resource.CloseAsync(1000, default);
        Assert.False(released);
        rawExit.Set();
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(3));
        if (stop == "close") await Assert.ThrowsAnyAsync<Exception>(() => request);
        await (closing ?? resource.CloseAsync(1000, default));
        Assert.False(nextSubmitted);
        Assert.True(released);
    }
}
