using System.Diagnostics;
using CAN.Models;
using PluginCommon;

namespace CAN.Runtime;

/// <summary>唯一接收者；FIFO 消费与快照观察互不抢帧。</summary>
internal sealed class CanReceiveRouter
{
    private readonly object _gate = new();
    private readonly Dictionary<uint, Queue<CanMessage>> _queues = new();
    private readonly HashSet<FeedbackSubscription> _subscriptions = [];
    private TaskCompletionSource _changed = NewSignal();
    private Exception? _error;
    private bool _stopped;
    private long _evictions;
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static CanMessage Copy(CanMessage frame) => new()
    {
        Id = frame.Id, Data = frame.Data.ToArray(), FrameType = frame.FrameType, IsFd = frame.IsFd,
        TimestampNs = frame.TimestampNs, TimestampDomain = frame.TimestampDomain, ClockId = frame.ClockId,
        RawTimestamp100ns = frame.RawTimestamp100ns,
        RawTimestampUs = frame.RawTimestampUs,
        IngressMonotonicTicks = frame.IngressMonotonicTicks,
        CaptureMonotonicLowerBoundTicks = frame.CaptureMonotonicLowerBoundTicks,
        IsTransmitEcho = frame.IsTransmitEcho
    };

    public void Publish(CanMessage frame)
    {
        if (frame.IsTransmitEcho || frame.IsFd || frame.FrameType != CanFrameType.Standard || frame.Id > 0x7FF) return;
        var copy = Copy(frame);
        copy.IngressMonotonicTicks ??= Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (_stopped) return;
            if (!_queues.TryGetValue(copy.Id, out var queue)) _queues[copy.Id] = queue = new();
            if (queue.Count == 64) { queue.Dequeue(); _evictions++; }
            queue.Enqueue(copy);
            foreach (var subscription in _subscriptions) subscription.Publish(copy);
            Pulse();
        }
    }

    public async Task<CanMessage?> ReadAsync(uint id, int timeoutMs, CancellationToken ct, OwnerStopSignal? stop = null)
    {
        if (id > 0x7FF || timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var clock = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            stop?.ThrowIfStopped("CANopen 接收");
            Task signal;
            lock (_gate)
            {
                if (_error != null) throw new InvalidOperationException("CANopen 后台接收失败", _error);
                if (_stopped) throw new InvalidOperationException("CANopen 接收已停止");
                if (_queues.TryGetValue(id, out var queue) && queue.TryDequeue(out var frame)) return Copy(frame);
                signal = _changed.Task;
            }
            var remaining = timeoutMs - clock.Elapsed.TotalMilliseconds;
            if (remaining <= 0) return null;
            try
            {
                if (stop == null) await signal.WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false);
                else await stop.WaitAsync(signal, TimeSpan.FromMilliseconds(remaining), ct, "CANopen 接收").ConfigureAwait(false);
            }
            catch (TimeoutException) { return null; }
        }
    }

    public FeedbackSubscription Subscribe(FeedbackRoute[] routes)
    {
        if (routes.Length == 0 || routes.Length > 128 || routes.Select(r => r.Name).Distinct().Count() != routes.Length ||
            routes.Select(r => r.CobId).Distinct().Count() != routes.Length ||
            routes.Any(r => string.IsNullOrWhiteSpace(r.Name) || r.CobId > 0x7FF || r.Dlc is < 0 or > 8 || r.MaxAgeMs <= 0))
            throw new ArgumentException("反馈路由要求唯一名称/COB-ID、DLC 0~8、正数有效期，最多 128 路");
        lock (_gate)
        {
            if (_stopped || _error != null) throw new InvalidOperationException("接收路由不可用", _error);
            var subscription = new FeedbackSubscription(routes.ToArray(), this);
            _subscriptions.Add(subscription);
            return subscription;
        }
    }

    public void Remove(FeedbackSubscription subscription) { lock (_gate) _subscriptions.Remove(subscription); }
    public void Discard(uint id)
    {
        lock (_gate) if (_queues.TryGetValue(id, out var queue)) queue.Clear();
    }
    public (int Depth, long Evictions, string? Error) Diagnostics
    { get { lock (_gate) return (_queues.Values.Sum(q => q.Count), _evictions, _error?.ToString()); } }
    public void Finish(Exception? error)
    {
        lock (_gate) { _error = error; _stopped = true; Pulse(); }
    }
    private void Pulse() { var old = _changed; _changed = NewSignal(); old.TrySetResult(); }
}

internal sealed class FeedbackSubscription(FeedbackRoute[] routes, CanReceiveRouter router) : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<uint, (CanMessage Frame, long Sequence, string? ClockError)> _latest = new();
    private bool _disposed;
    public Guid Epoch { get; } = Guid.NewGuid();
    public long Replacements { get; private set; }

    internal void Publish(CanMessage frame)
    {
        if (!routes.Any(r => r.CobId == frame.Id)) return;
        lock (_gate)
        {
            if (_disposed) return;
            var seen = _latest.TryGetValue(frame.Id, out var old);
            if (seen) Replacements++;
            var outOfOrder = seen && frame.ClockId == old.Frame.ClockId && frame.TimestampDomain != "Unknown" &&
                frame.TimestampNs <= old.Frame.TimestampNs;
            var clockReset = seen && (frame.ClockId != old.Frame.ClockId || frame.TimestampDomain != old.Frame.TimestampDomain);
            _latest[frame.Id] = (frame, seen ? old.Sequence + 1 : 1, clockReset ? "ClockReset" : outOfOrder ? "OutOfOrder" : null);
        }
    }

    public FeedbackSnapshot[] Snapshot()
    {
        lock (_gate)
            return routes.Select(route =>
            {
                if (!_latest.TryGetValue(route.CobId, out var value))
                    return new FeedbackSnapshot(route.Name, route.CobId, route.Dlc, null, "", 0, null,
                        "Unknown", "Unknown", null, null, false, _disposed ? "Stopped" : "NeverReceived");
                var frame = value.Frame;
                double? age = frame.CaptureMonotonicLowerBoundTicks is long capture && capture <= Stopwatch.GetTimestamp()
                    ? Stopwatch.GetElapsedTime(capture).TotalMilliseconds : null;
                var quality = _disposed ? "Stopped" : frame.Dlc != route.Dlc ? "LengthMismatch" :
                    value.ClockError ?? (age == null ? "TimeUnknown" : age > route.MaxAgeMs ? "Stale" : "Valid");
                return new FeedbackSnapshot(route.Name, route.CobId, route.Dlc, frame.Dlc,
                    Convert.ToHexString(frame.Data), value.Sequence, frame.TimestampNs, frame.TimestampDomain,
                    frame.ClockId, frame.IngressMonotonicTicks, age, age.HasValue, quality)
                { RawTimestamp100ns = frame.RawTimestamp100ns, RawTimestampUs = frame.RawTimestampUs,
                    CaptureMonotonicLowerBoundTicks = frame.CaptureMonotonicLowerBoundTicks };
            }).ToArray();
    }
    public void Dispose()
    {
        // 从路由移除后再拿订阅锁，保持 Publish 的锁顺序。
        router.Remove(this);
        lock (_gate) _disposed = true;
    }
}
