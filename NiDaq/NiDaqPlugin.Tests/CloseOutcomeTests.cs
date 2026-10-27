using System.IO;
using NiDaq.Helpers;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class CloseOutcomeTests
{
    [Fact]
    public async Task Completed_abort_failure_does_not_prevent_final_dispose()
    {
        int aborts = 0, releases = 0;
        var owner = new DaqTaskResource(
            () => { Interlocked.Increment(ref aborts); throw new IOException("abort returned failure"); },
            () => Interlocked.Increment(ref releases));
        await owner.ConfigureAsync(() => { }, default);

        await owner.CloseAsync(2000, default);
        await owner.CloseAsync(2000, default);

        Assert.True(owner.IsClosed);
        Assert.Equal(1, aborts);
        Assert.Equal(1, releases);
    }

    [Fact]
    public async Task Actual_dispose_failure_is_not_hidden_by_abort_failure()
    {
        int releases = 0;
        bool releaseFails = true;
        var owner = new DaqTaskResource(() => throw new IOException("abort returned failure"), () =>
        {
            Interlocked.Increment(ref releases);
            if (releaseFails) throw new IOException("dispose returned failure");
        });
        await owner.ConfigureAsync(() => { }, default);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync(2000, default));
        Assert.Contains("dispose returned failure", failure.ToString());
        Assert.Contains("abort returned failure", failure.ToString());
        Assert.False(owner.IsClosed);
        Assert.Equal(1, releases);
        releaseFails = false;
        await owner.CloseAsync(2000, default);
        Assert.True(owner.IsClosed);
        Assert.Equal(2, releases);
    }
}
