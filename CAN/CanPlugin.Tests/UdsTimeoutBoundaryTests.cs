using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;
using CAN.UDS;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class UdsTimeoutBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResponseDeadlineReturnsTimeoutWhileLateNativeReadRemainsOwned(bool useOwner)
    {
        using var raw = new BlockedReadAdapter();
        var owner = useOwner ? new CanConnectionResource(raw) : null;
        ICanAdapter adapter = owner ?? (ICanAdapter)raw;
        var request = new UdsClient(adapter, 1, 2, responseTimeoutMs: 200).RequestAsync([0x22, 0xF1, 0x89]);
        try
        {
            await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var response = await request.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(response.IsTimeout);
            Assert.False(response.IsPositive);
            Assert.Contains("200", response.RequestDescription);
            Assert.False(raw.ReadExited.Task.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new UdsClient(adapter, 1, 2).RequestAsync([0x22, 0xF1, 0x89]));
            Assert.Equal(1, raw.Writes);
            if (owner != null)
            {
                await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
                Assert.False(owner.IsClosed);
                Assert.Equal(0, raw.Closes);
            }
        }
        finally
        {
            raw.Release.Set();
            await raw.ReadExited.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (owner != null) await owner.CloseAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => new UdsClient(adapter, 1, 2).RequestAsync([0x22, 0xF1, 0x89]));
        Assert.Equal(1, raw.Writes);
    }

    [Fact]
    public async Task WholeTransactionDeadlineIsNotConvertedToAnEcuResponseTimeout()
    {
        using var raw = new BlockedReadAdapter();
        var owner = new CanConnectionResource(raw);
        var request = new UdsClient(owner, 1, 2, responseTimeoutMs: 5000, transactionTimeoutMs: 200)
            .RequestAsync([0x22, 0xF1, 0x89]);
        try
        {
            await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => request);
            Assert.Contains("事务", error.Message);
            Assert.False(raw.ReadExited.Task.IsCompleted);
            Assert.Equal(1, raw.Writes);
        }
        finally { raw.Release.Set(); await raw.ReadExited.Task.WaitAsync(TimeSpan.FromSeconds(3)); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task NullReadLimitedByTransactionBudgetIsNotAResponseWindowTimeout()
    {
        var raw = new BlockedReadAdapter { ReturnNull = true };
        raw.Release.Set();
        try
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(() => new UdsClient(raw, 1, 2,
                responseTimeoutMs: 5000, transactionTimeoutMs: 1000).RequestAsync([0x22, 0xF1, 0x89]));
            Assert.Contains("事务", error.Message);
        }
        finally { raw.Dispose(); }
    }

    [Fact]
    public async Task CallerCancellationDuringNativeReadRemainsCancellation()
    {
        using var raw = new BlockedReadAdapter();
        using var caller = new CancellationTokenSource();
        var owner = new CanConnectionResource(raw);
        var request = new UdsClient(owner, 1, 2).RequestAsync([0x22, 0xF1, 0x89], caller.Token);
        try
        {
            await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            Assert.False(raw.ReadExited.Task.IsCompleted);
            Assert.Equal(0, raw.Closes);
        }
        finally { raw.Release.Set(); await raw.ReadExited.Task.WaitAsync(TimeSpan.FromSeconds(3)); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task RequestWriteTimeoutIsNotConvertedToAnEcuResponseTimeout()
    {
        var raw = new FakeAdapter { OnWrite = _ => throw new TimeoutException("native write failed") };
        var error = await Assert.ThrowsAsync<TimeoutException>(() => new UdsClient(raw, 1, 2).RequestAsync([0x22, 0xF1, 0x89]));
        Assert.Contains("native write failed", error.ToString());
        Assert.False(raw.ReadEntered.Task.IsCompleted);
    }

    private sealed class BlockedReadAdapter : ICanAdapter
    {
        internal readonly ManualResetEventSlim Release = new();
        internal readonly TaskCompletionSource ReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReadExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Writes, Closes;
        internal bool ReturnNull;
        public bool IsConnected => Closes == 0;
        public void Open(CanAdapterConfig config) { }
        public void Write(CanMessage frame) => Interlocked.Increment(ref Writes);
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default)
        {
            ReadEntered.TrySetResult();
            try
            {
                Release.Wait(); // Deliberately ignores the soft deadline and cancellation.
                return ReturnNull ? null : new() { Id = 2, Data = [4, 0x62, 0xF1, 0x89, 9] };
            }
            finally { ReadExited.TrySetResult(); }
        }
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
        public void Close() => Interlocked.Increment(ref Closes);
        public void Dispose() => Release.Dispose();
    }
}
