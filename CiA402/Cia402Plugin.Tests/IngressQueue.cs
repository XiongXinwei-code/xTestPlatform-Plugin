using System.Collections.Concurrent;
using System.Diagnostics;
using CAN.Models;

namespace CiA402.Tests;

public sealed class IngressQueue : ConcurrentQueue<CanMessage>
{
    public new void Enqueue(CanMessage frame)
    {
        frame.IngressMonotonicTicks ??= Stopwatch.GetTimestamp();
        // This fake is the producer: enqueue is the simulated acquisition event,
        // unlike dequeue/driver ingress in production.
        frame.CaptureMonotonicLowerBoundTicks ??= frame.IngressMonotonicTicks;
        base.Enqueue(frame);
    }
}
