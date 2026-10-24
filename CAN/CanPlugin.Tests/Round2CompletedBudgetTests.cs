using System.Reflection;
using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;
using CAN.UDS;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class Round2CompletedBudgetTests
{
    [Theory]
    [InlineData("Write")]
    [InlineData("Read")]
    [InlineData("SendOnly")]
    public async Task CompletedIoWithHeldReturnContinuationCannotPublishAfterTotalDeadline(string operation)
    {
        using var resume = new ManualResetEventSlim();
        using var returnNative = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool completed = false;
        int held = 0;
        var flow = new AsyncLocal<bool>(change =>
        {
            // WaitAsync has already selected the successful original task. Hold its
            // actual return continuation, not native entry and not the test caller.
            if (change.ThreadContextChanged && change.CurrentValue && Volatile.Read(ref completed) && Interlocked.CompareExchange(ref held, 1, 0) == 0)
            {
                paused.TrySetResult();
                // Notification callbacks must never throw (ExecutionContext would FailFast).
                resume.Wait(5000);
            }
        });
        var raw = new ReturnAdapter(() =>
        {
            entered.TrySetResult();
            if (!returnNative.Wait(5000)) throw new TimeoutException("test native return not released");
            Volatile.Write(ref completed, true); flow.Value = false;
        });
        var owner = new CanConnectionResource(raw);
        Task pending;
        flow.Value = true;
        try
        {
            pending = operation switch
            {
                "Write" => owner.WriteAsync(new() { Id = 1, Data = [1] }, 1000),
                "Read" => owner.ReadAsync(null, 1000),
                _ => new UdsClient(owner, 1, 2, transactionTimeoutMs: 1000).SendOnlyAsync([0x3E, 0x80])
            };
        }
        finally { flow.Value = false; }
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            returnNative.Set();
            await paused.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(completed); Assert.False(pending.IsCompleted);
            await Task.Delay(1100);
            resume.Set();
            await Assert.ThrowsAsync<TimeoutException>(() => pending);
        }
        finally { Interlocked.Exchange(ref held, 1); returnNative.Set(); resume.Set(); try { await pending; } catch { } await owner.CloseAsync(); }
    }

    private sealed class ReturnAdapter(Action complete) : CAN.Adapters.ICanAdapter
    {
        public bool IsConnected => true;
        public void Open(CanAdapterConfig config) { }
        public void Close() { }
        public void Dispose() { }
        public void Write(CanMessage frame) => complete();
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) { complete(); return new() { Id = 1, Data = [1] }; }
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
    }
}
