using System.Collections.Concurrent;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanSubmissionGateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queue_is_fifo_except_for_same_timestamp_head_id_ties(bool simultaneous)
    {
        long now = 1;
        var gate = new CanSubmissionGate(() => Volatile.Read(ref now));
        var order = new ConcurrentQueue<int>();
        var first = gate.Enter(null, default, () => { }, 1);
        Task second = Task.Run(() => { using var turn = gate.Enter(null, default, () => { }, 0x700); order.Enqueue(0x700); });
        await Until(() => gate.QueuedCount == 1);
        if (!simultaneous) Interlocked.Increment(ref now);
        Task third = Task.Run(() => { using var turn = gate.Enter(null, default, () => { }, 0x80); order.Enqueue(0x80); });
        try { await Until(() => gate.QueuedCount == 2); }
        finally { first.Dispose(); }
        await Task.WhenAll(second, third).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(simultaneous ? new[] { 0x80, 0x700 } : new[] { 0x700, 0x80 }, order);
    }

    [Fact]
    public async Task Cancelled_waiter_cannot_get_a_late_turn_or_block_its_successor()
    {
        var gate = new CanSubmissionGate(); using var first = gate.Enter(null, default, () => { }, 1);
        using var cancelled = new CancellationTokenSource(); int entered = 0;
        var old = Task.Run(() => { using var turn = gate.Enter(null, cancelled.Token, () => { }, 2); Interlocked.Increment(ref entered); });
        await Until(() => gate.QueuedCount == 1);
        var next = Task.Run(() => { using var turn = gate.Enter(null, default, () => { }, 3); Interlocked.Add(ref entered, 10); });
        await Until(() => gate.QueuedCount == 2);
        cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old);
        Assert.Equal(1, gate.QueuedCount); first.Dispose();
        await next.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(10, entered);
    }

    private static async Task Until(Func<bool> ready)
    { using var guard = new CancellationTokenSource(5000); while (!ready()) await Task.Delay(2, guard.Token); }
}
