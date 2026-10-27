using System.IO;
using VISA.Helpers;
using Xunit;

namespace VISA.Tests;

public sealed class CloseOutcomeTests
{
    [Fact]
    public async Task A_second_noop_dispose_is_not_proof_that_failed_sdk_release_succeeded()
    {
        var handle = new NoopAfterFailure();
        var owner = new VisaSessionResource(2000, "\n");
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync(() => handle, _ => { }, 2000, default));
        await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync(2000));

        await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync(2000));
        Assert.False(owner.IsClosed);
        Assert.False(handle.Released);
        Assert.Equal(1, handle.Disposes);
    }

    private sealed class NoopAfterFailure : IDisposable
    {
        internal int Disposes;
        internal bool Released => false;
        private bool _disposed;
        public void Dispose()
        {
            Interlocked.Increment(ref Disposes);
            if (_disposed) return;
            _disposed = true;
            throw new IOException("SDK marked disposed but physical release failed");
        }
    }
}
