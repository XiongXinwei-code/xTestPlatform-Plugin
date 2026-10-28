using CiA402.Models;
using CiA402.Transport;

namespace CiA402.Tests;

internal sealed class ProbeAxisTransport : ICia402AxisTransport
{
    private int _active;
    private int _maximumActive;
    private int _calls;
    private int _disposeCount;
    public FakeAxisTransport Drive { get; } = new();
    public bool SupportsConcurrentMonitoring => true;
    public bool FeedbackIsPassiveReceive { get; set; }
    public Func<bool, bool, bool, CancellationToken, Task<AxisFeedback>>? FeedbackHandler { get; set; }
    public Func<ushort, CancellationToken, Task<byte[]>>? ReadHandler { get; set; }
    public Func<ushort, byte[], CancellationToken, Task>? WriteHandler { get; set; }
    public Action? DisposeAction { get; set; }
    public int MaximumActive => Volatile.Read(ref _maximumActive);
    public int Calls => Volatile.Read(ref _calls);
    public int DisposeCount => Volatile.Read(ref _disposeCount);
    public bool DisposedDuringCall { get; private set; }
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task<T> Track<T>(Func<Task<T>> operation)
    {
        Interlocked.Increment(ref _calls);
        var active = Interlocked.Increment(ref _active);
        int previous;
        do { previous = Volatile.Read(ref _maximumActive); }
        while (active > previous && Interlocked.CompareExchange(ref _maximumActive, active, previous) != previous);
        try { return await operation().ConfigureAwait(false); }
        finally { Interlocked.Decrement(ref _active); }
    }

    public Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct)
        => Track(() => ReadHandler != null ? ReadHandler(index, ct) : Drive.ReadObjectAsync(index, subIndex, ct));
    public Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct)
        => Track(async () =>
        {
            if (WriteHandler != null) await WriteHandler(index, value, ct);
            else await Drive.WriteObjectAsync(index, subIndex, value, ct);
            return true;
        });
    public Task WriteTargetAsync(OperationMode mode, long target, ushort controlWord, CancellationToken ct)
        => Track(async () => { await Drive.WriteTargetAsync(mode, target, controlWord, ct); return true; });
    public Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct)
        => Track(() => FeedbackHandler != null ? FeedbackHandler(position, velocity, torque, ct) : Drive.ReadFeedbackAsync(position, velocity, torque, ct));
    public void RequireCyclicMode(OperationMode mode) => Drive.RequireCyclicMode(mode);
    public void Dispose()
    {
        DisposedDuringCall |= Volatile.Read(ref _active) != 0;
        Interlocked.Increment(ref _disposeCount);
        DisposeAction?.Invoke();
        Drive.Dispose();
        Disposed.TrySetResult();
    }
}
