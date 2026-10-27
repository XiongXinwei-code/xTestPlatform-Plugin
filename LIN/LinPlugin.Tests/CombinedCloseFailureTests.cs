using LIN.Adapters;
using LIN.Models;
using LIN.Runtime;
using PluginCommon;
using Xunit;

namespace LIN.Tests;

[Collection("LIN managed retirement")]
public sealed class CombinedCloseFailureTests
{
    [Fact]
    public async Task Callback_and_final_close_failure_remain_visible_until_physical_retry_succeeds()
    {
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string physical = "LIN:NI:" + Guid.NewGuid().ToString();
        var raw = new Driver { FailClose = true }; var owner = new LinConnectionResource(raw, physical);
        CancellationTokenRegistration callback = default;
        var job = owner.StartJob([token =>
        {
            callback = token.Register(() => throw new InvalidOperationException("LIN fake cancellation callback failure"));
            registered.TrySetResult(); return Task.CompletedTask;
        }]);
        try
        {
            await registered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<AggregateException>(() => owner.CloseAsync(1000));
            Assert.Contains("LIN fake cancellation callback failure", error.ToString());
            Assert.Contains("LIN fake Close failure", error.ToString());
            Assert.Contains("未确认", error.Message); Assert.False(owner.IsClosed); Assert.False(raw.HandlesReleased);
            await Assert.ThrowsAnyAsync<Exception>(() => RetiringResources.WaitForPreviousAsync(physical, 1000));
            Assert.False(owner.IsClosed);
            raw.FailClose = false;
            await owner.CloseAsync(1000);
            Assert.Contains("LIN fake cancellation callback failure", owner.CloseWarning!.ToString());
            Assert.True(owner.IsClosed); Assert.True(raw.HandlesReleased);
            await RetiringResources.WaitForPreviousAsync(physical, 1000);
        }
        finally
        {
            raw.FailClose = false;
            if (!owner.IsClosed) try { await owner.CloseAsync(1000); } catch (Exception) when (owner.IsClosed) { }
            await Assert.ThrowsAnyAsync<Exception>(() => job.Completion);
            callback.Dispose();
        }
    }

    private sealed class Driver : ILinAdapter, ILinCloseState
    {
        internal bool FailClose;
        public bool HandlesReleased { get; private set; }
        public bool IsConnected => !HandlesReleased;
        public void Open(LinAdapterConfig config) { }
        public void Close()
        {
            if (FailClose) throw new InvalidOperationException("LIN fake Close failure");
            HandlesReleased = true;
        }
        public void Dispose() => Close();
        public void Write(LinFrame frame) { }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => null;
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => null;
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { }
    }
}
