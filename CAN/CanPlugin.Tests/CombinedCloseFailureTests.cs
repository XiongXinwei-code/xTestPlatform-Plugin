using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CombinedCloseFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Callback_and_driver_failure_are_preserved_without_releasing_unknown_handles(bool failDispose)
    {
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new Driver { Fail = true, FailDispose = failDispose };
        string key = Guid.NewGuid().ToString(); var owner = new CanConnectionResource(raw, key);
        CancellationTokenRegistration callback = default;
        var job = owner.StartJob("callback", token =>
        {
            callback = token.Register(() => throw new InvalidOperationException("CAN fake cancellation callback failure"));
            registered.TrySetResult(); return Task.CompletedTask;
        });
        try
        {
            await registered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<AggregateException>(() => owner.CloseAsync(1000));
            Assert.Contains("CAN fake cancellation callback failure", error.ToString());
            Assert.Contains(failDispose ? "CAN fake Dispose failure" : "CAN fake Close failure", error.ToString());
            Assert.False(owner.IsClosed); Assert.False(raw.HandlesReleased);
            Assert.Contains("未确认", error.Message);
            Assert.Throws<InvalidOperationException>(() => new CanConnectionResource(new Driver(), key));
            raw.Fail = false;
            var historical = await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync(1000));
            Assert.Contains("CAN fake cancellation callback failure", historical.ToString());
            Assert.True(owner.IsClosed); Assert.True(raw.HandlesReleased);
            await CanConnectionResource.WaitForPreviousOwnerAsync(key, 1000);
        }
        finally
        {
            raw.Fail = false;
            if (!owner.IsClosed) try { await owner.CloseAsync(1000); } catch (Exception) when (owner.IsClosed) { }
            await Assert.ThrowsAnyAsync<Exception>(() => job.Completion);
            callback.Dispose();
        }
    }

    private sealed class Driver : ICanAdapter, ICanCloseState
    {
        internal bool Fail, FailDispose;
        private bool _closeReturned;
        public bool HandlesReleased { get; private set; }
        public bool IsConnected => !HandlesReleased;
        public void Open(CanAdapterConfig config) { }
        public void Close()
        {
            if (Fail && !FailDispose) throw new InvalidOperationException("CAN fake Close failure");
            _closeReturned = true;
        }
        public void Dispose()
        {
            if (Fail && FailDispose) throw new InvalidOperationException("CAN fake Dispose failure");
            HandlesReleased = _closeReturned;
        }
        public void Write(CanMessage message) { }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
    }
}
