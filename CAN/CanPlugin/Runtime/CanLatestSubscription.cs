using System.Diagnostics;
using CAN.Models;

namespace CAN.Runtime;

internal sealed record CanLatestRoute(uint Id, CanFrameType FrameType, CanReceiveFrameFormat Format, int MaxAgeMs)
{
    internal bool Matches(CanMessage frame) => frame.Id == Id && frame.FrameType == FrameType &&
        (Format == CanReceiveFrameFormat.Any || frame.IsFd == (Format == CanReceiveFrameFormat.Fd));
}
internal sealed record CanLatestFrame(CanLatestRoute Route, CanMessage? Frame, long Sequence, bool Valid,
    string Quality, double? AgeUpperBoundMs);
internal sealed record CanLatestSnapshot(CanLatestFrame[] Frames, long ReceivedFrames, long Replacements,
    bool Running, string? Error);

/// <summary>独立有界观察器，每条配置只留最新帧，不消费原始读取和 CANopen 队列。</summary>
internal sealed class CanLatestSubscription(CanLatestRoute[] routes, Action<CanLatestSubscription> remove) : IDisposable
{
    private readonly object _gate = new();
    private readonly CanMessage?[] _frames = new CanMessage?[routes.Length];
    private readonly long[] _sequences = new long[routes.Length];
    private readonly string?[] _clockErrors = new string?[routes.Length];
    private bool _stopped;
    private Exception? _error;
    private long _received, _replacements;

    internal void Publish(CanMessage frame)
    {
        lock (_gate)
        {
            if (_stopped) return;
            for (int i = 0; i < routes.Length; i++)
            {
                if (!routes[i].Matches(frame)) continue;
                var previous = _frames[i];
                _clockErrors[i] = previous == null ? null :
                    previous.ClockId != frame.ClockId || previous.TimestampDomain != frame.TimestampDomain ? "ClockReset" :
                    frame.TimestampDomain != "Unknown" && frame.TimestampNs <= previous.TimestampNs ? "OutOfOrder" : null;
                if (previous != null) _replacements++;
                _frames[i] = frame; _sequences[i]++; _received++;
            }
        }
    }

    internal CanLatestSnapshot Snapshot()
    {
        lock (_gate)
        {
            var items = new CanLatestFrame[routes.Length];
            long now = Stopwatch.GetTimestamp();
            for (int i = 0; i < routes.Length; i++)
            {
                var frame = _frames[i];
                double? age = frame?.CaptureMonotonicLowerBoundTicks is long capture && capture <= now && capture > 0
                    ? (now - capture) * 1000d / Stopwatch.Frequency : null;
                string quality = _stopped ? "Stopped" : frame == null ? "NeverReceived" :
                    _clockErrors[i] ?? (age == null ? "TimeUnknown" : age > routes[i].MaxAgeMs ? "Stale" : "Valid");
                items[i] = new(routes[i], frame == null ? null : CanReceiveRouter.Copy(frame), _sequences[i],
                    quality == "Valid", quality, age);
            }
            return new(items, _received, _replacements, !_stopped, _error?.ToString());
        }
    }

    internal void Finish(Exception? error) { lock (_gate) { _stopped = true; _error ??= error; } }
    public void Dispose() { remove(this); Finish(null); }
}
