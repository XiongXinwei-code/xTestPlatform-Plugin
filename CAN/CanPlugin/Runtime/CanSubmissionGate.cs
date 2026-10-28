using System.Diagnostics;

namespace CAN.Runtime;

/// <summary>整批尚未获准进入SDK，已由其所有者撤销；不代表已发送报文撤回。</summary>
internal sealed class CanBatchWithdrawnException() : OperationCanceledException("发送源已冻结，整批尚未进入SDK");

/// <summary>连接级有界准入队列；只有同一单调时刻的待准入单元按首帧ID决胜。</summary>
internal sealed class CanSubmissionGate(Func<long>? clock = null)
{
    private sealed class Waiter(long at, uint priority)
    {
        internal readonly long At = at;
        internal readonly uint Priority = priority;
        internal readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<Waiter>? Node;
    }
    private readonly object _gate = new();
    private readonly LinkedList<Waiter> _pending = new();
    private Waiter? _current;
    internal int QueuedCount { get { lock (_gate) return _pending.Count; } }

    internal IDisposable Enter(CanConfirmationBudget? budget, CancellationToken ct, Action admission, uint headId)
    {
        admission(); ct.ThrowIfCancellationRequested();
        if (budget is { } initial) _ = initial.Remaining(ct);
        Waiter waiter;
        lock (_gate)
        {
            if (_pending.Count >= 1024) throw new InvalidOperationException("CAN 连接提交队列已满（1024），未进入SDK");
            waiter = new((clock ?? Stopwatch.GetTimestamp)(), headId);
            if (_current == null) { _current = waiter; waiter.Ready.SetResult(); }
            else
            {
                var before = _pending.Last;
                while (before != null && before.Value.At == waiter.At && before.Value.Priority > waiter.Priority) before = before.Previous;
                waiter.Node = before == null ? _pending.AddFirst(waiter) : _pending.AddAfter(before, waiter);
            }
        }
        try
        {
            if (budget is { } bounded) waiter.Ready.Task.WaitAsync(TimeSpan.FromMilliseconds(bounded.Remaining(ct)), ct).GetAwaiter().GetResult();
            else waiter.Ready.Task.WaitAsync(ct).GetAwaiter().GetResult();
            admission(); ct.ThrowIfCancellationRequested();
            if (budget is { } result) _ = result.Remaining(ct);
            return new Lease(this, waiter);
        }
        catch
        {
            lock (_gate)
            {
                if (waiter.Node is { } node) { _pending.Remove(node); waiter.Node = null; waiter.Ready.TrySetCanceled(); }
                else ReleaseLocked(waiter); // Cancellation raced a real grant; return that exact turn.
            }
            throw;
        }
    }
    private void ReleaseLocked(Waiter waiter)
    {
        if (!ReferenceEquals(_current, waiter)) return;
        if (_pending.First is { } next)
        {
            _pending.RemoveFirst(); next.Value.Node = null; _current = next.Value;
            next.Value.Ready.TrySetResult();
        }
        else _current = null;
    }
    private sealed class Lease(CanSubmissionGate gate, Waiter waiter) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (gate._gate) gate.ReleaseLocked(waiter);
        }
    }
}
