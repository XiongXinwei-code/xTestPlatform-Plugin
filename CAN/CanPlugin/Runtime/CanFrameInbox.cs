using System.Diagnostics;
using CAN.Models;
using PluginCommon;

namespace CAN.Runtime;

/// <summary>独立、有界消费副本；保留原采集时间，不触碰其它协议的队列。</summary>
internal sealed class CanFrameInbox(Action<CanFrameInbox>? remove = null) : IDisposable
{
    internal const int Capacity = 4096;
    private readonly object _gate = new();
    private readonly LinkedList<CanMessage> _frames = new();
    private TaskCompletionSource _changed = Signal();
    private bool _stopped;
    private Exception? _error;
    internal long Evictions { get; private set; }
    internal int Count { get { lock (_gate) return _frames.Count; } }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal void Publish(CanMessage immutableFrame)
    {
        lock (_gate)
        {
            if (_stopped) return;
            if (_frames.Count == Capacity) { _frames.RemoveFirst(); Evictions++; }
            _frames.AddLast(immutableFrame); Pulse();
        }
    }
    internal async Task<CanMessage?> ReadAsync(uint? id, int timeoutMs, CancellationToken ct, OwnerStopSignal? stop = null)
    {
        if (timeoutMs < Timeout.Infinite) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        long started = Stopwatch.GetTimestamp();
        while (true)
        {
            ct.ThrowIfCancellationRequested(); stop?.ThrowIfStopped("共享CAN读取");
            Task changed;
            lock (_gate)
            {
                if (_error != null) throw new InvalidOperationException("共享CAN接收后台失败", _error);
                if (_stopped) throw new InvalidOperationException("共享CAN接收副本已关闭");
                if (timeoutMs > 0 && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs) return null;
                var next = _frames.First;
                while (next != null && id.HasValue && next.Value.Id != id.Value) next = next.Next;
                if (next != null) { _frames.Remove(next); return CanReceiveRouter.Copy(next.Value); }
                changed = _changed.Task;
            }
            double remaining = timeoutMs == Timeout.Infinite ? Timeout.Infinite : timeoutMs - Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (timeoutMs != Timeout.Infinite && remaining <= 0) return null;
            var wait = TimeSpan.FromMilliseconds(remaining);
            try
            {
                if (stop == null) await changed.WaitAsync(wait, ct).ConfigureAwait(false);
                else await stop.WaitAsync(changed, wait, ct, "共享CAN读取").ConfigureAwait(false);
            }
            catch (TimeoutException) { return null; }
        }
    }
    internal void Finish(Exception? error)
    { lock (_gate) { _stopped = true; _error ??= error; _frames.Clear(); Pulse(); } }
    private void Pulse() { var previous = _changed; _changed = Signal(); previous.TrySetResult(); }
    public void Dispose() { remove?.Invoke(this); Finish(null); }
}
