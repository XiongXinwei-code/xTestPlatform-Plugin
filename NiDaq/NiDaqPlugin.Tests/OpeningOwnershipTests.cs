using AuditTestSupport;
using System.IO;
using NiDaq.Helpers;
using Xunit;
namespace NiDaq.ContractTests;
public class OpeningOwnershipTests
{
    [Fact]
    public async Task StopCanFindAndFreezeAnInProgressConfiguration()
    {
        var context = ContextProxy.Create();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var exit = new ManualResetEventSlim();
        var configuring = DaqTaskFactory.CreateAndRegisterAsync(context, "A", _ => { entered.TrySetResult(); exit.Wait(); }, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var owner = NiDaqTaskRegistry.Get(context, "A");
            var closing = owner.CloseAsync(1000, default);
            exit.Set();
            await closing;
            await Assert.ThrowsAnyAsync<Exception>(() => configuring);
            Assert.Throws<InvalidOperationException>(() => NiDaqTaskRegistry.Get(context, "A"));
        }
        finally
        {
            exit.Set();
            await configuring.ContinueWith(task => { _ = task.Exception; });
        }
    }

    [Fact]
    public async Task FailedConfigurationCleanupStaysRegisteredUntilRetryReallyCloses()
    {
        var context = ContextProxy.Create();
        bool failRelease = true;
        var owner = new DaqTaskResource(() => { }, () => { if (failRelease) throw new IOException("fake release"); });
        await Assert.ThrowsAnyAsync<Exception>(() => DaqTaskFactory.CreateAndRegisterAsync(context, "A", _ => throw new InvalidOperationException("fake configure"), default, owner));
        Assert.Same(owner, NiDaqTaskRegistry.Get(context, "A"));
        failRelease = false;
        await owner.CloseAsync(1000, default);
        NiDaqTaskRegistry.RemoveClosed(context, "A", owner);
        Assert.Throws<InvalidOperationException>(() => NiDaqTaskRegistry.Get(context, "A"));
    }
}
