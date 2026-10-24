using NiDaq.Helpers;
using System.IO;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ImmediateRerunTests
{
    [Fact]
    public async Task NewExecutionWaitsForRetiringPhysicalChannelsBeforeConfiguration()
    {
        string physical = "FakeDevice" + Guid.NewGuid().ToString("N") + "/ai0:1";
        bool released = false, configured = false;
        var old = new DaqTaskResource(() => { }, () => released = true, [physical]);
        var next = new DaqTaskResource(() => { }, () => { }, [physical.Replace("ai0:1", "ai1")]);
        await old.ConfigureAsync(() => { }, default);
        var raw = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(); using var cancel = new CancellationTokenSource();
        var pending = old.RunAsync(() => { entered.TrySetResult(); release.Wait(); return 1; }, 2000, "old read", cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); old.Dispose();
        var opening = next.ConfigureAsync(() => { if (!released) throw new IOException("DAQ physical channel busy"); configured = true; }, default);
        try
        {
            await Task.WhenAny(opening, Task.Delay(100)); Assert.False(opening.IsCompleted);
            release.Set(); await opening; Assert.True(configured);
        }
        finally { release.Set(); await old.CloseAsync(2000, default); await next.CloseAsync(2000, default); }
    }
}
