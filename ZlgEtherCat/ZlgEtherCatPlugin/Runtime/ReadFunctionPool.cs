using System.Diagnostics;

namespace ZlgEtherCat.Runtime;

public sealed record InputSnapshot(long Sequence, long ReceivedTimestamp, byte[] Data, uint QueueBacklog, bool LocallyValid, bool HasQueueEvent = true)
{
    /// <summary>主机读取完成后的年龄，不包含 SDK RX 队列中此前的等待时间。</summary>
    public double LocalAgeMs => Stopwatch.GetElapsedTime(ReceivedTimestamp).TotalMilliseconds;
    public string TimestampSource => HostReadTimestamp.TimestampSource;
    public long TimestampFrequencyHz => HostReadTimestamp.TimestampFrequencyHz;
    public bool HardwareTimestampAvailable => HostReadTimestamp.HardwareTimestampAvailable;
    public long? HardwareTimestamp => HostReadTimestamp.HardwareTimestamp;
}

public sealed record ReadItemStatus(string Name, bool Enabled, long Publications, long LastSequence, string? Error, long SkippedSamples);

/// <summary>每池固定一个工作循环；读取项仅是配置，绝不为读取项创建线程、任务或计时器。</summary>
public sealed class ReadFunctionPool : IDisposable
{
    private sealed class Item(string name, int intervalMs, Action<InputSnapshot> publish)
    {
        public readonly string Name = name;
        public readonly int IntervalMs = intervalMs;
        public readonly Action<InputSnapshot> Publish = publish;
        public bool Enabled = true;
        public bool Entered;
        public long LastSequence = -1;
        public long Publications;
        public long SkippedSamples;
        public long NextDue;
        public string? Error;
        public readonly TaskCompletionSource Drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly object _sync = new();
    private readonly Dictionary<string, Item> _items = new(StringComparer.Ordinal);
    private readonly AutoResetEvent _wake = new(false);
    private readonly bool _periodic;
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private InputSnapshot? _latest;
    private bool _closing;
    private Action? _maintenance;
    public int WorkerStarts { get; private set; }
    public int Count { get { lock (_sync) return _items.Count; } }
    public Task Completion => _exited.Task;

    // Reuse this fixed worker for bounded diagnostic delivery; never create another task or timer.
    internal void SetMaintenance(Action maintenance)
    {
        ArgumentNullException.ThrowIfNull(maintenance);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            _maintenance = maintenance;
            _wake.Set();
        }
    }

    internal void WakeMaintenance()
    {
        lock (_sync) if (!_closing) _wake.Set();
    }

    public ReadFunctionPool(bool periodic)
    {
        _periodic = periodic;
        var worker = new Thread(Run) { IsBackground = true, Name = periodic ? "ZLG 周期读取池" : "ZLG 常驻读取池" };
        WorkerStarts = 1;
        // A long-lived pool must never inherit a single step's AsyncLocal execution binding.
        if (ExecutionContext.IsFlowSuppressed()) worker.Start();
        else { using (ExecutionContext.SuppressFlow()) worker.Start(); }
    }

    public Action Add(string name, int intervalMs, Action<InputSnapshot> publish)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(publish);
        if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs), "读取周期必须大于零");
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            if (_items.Count >= 1024) throw new InvalidOperationException("读取池最多允许 1024 个配置项");
            var item = new Item(name, intervalMs, publish);
            if (!_items.TryAdd(name, item)) throw new InvalidOperationException($"读取项 {name} 已存在或正在停止");
            _wake.Set();
            return () =>
            {
                lock (_sync)
                {
                    if (!_items.TryGetValue(name, out var current) || !ReferenceEquals(current, item)) return;
                    item.Enabled = false;
                    if (!item.Entered) item.Drained.TrySetResult();
                    if (!_closing) _wake.Set();
                }
            };
        }
    }

    public void Publish(InputSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_closing) return;
            _latest = snapshot;
            _wake.Set();
        }
    }

    public async Task RemoveAsync(string name, CancellationToken token)
    {
        Item item;
        lock (_sync)
        {
            if (!_items.TryGetValue(name, out item!)) throw new InvalidOperationException($"读取项 {name} 不存在");
            item.Enabled = false;
            if (!item.Entered) item.Drained.TrySetResult();
            if (!_closing) _wake.Set();
        }
        // Cancellation ends only this wait. The tombstone continues preventing duplicate registration.
        await item.Drained.Task.WaitAsync(token).ConfigureAwait(false);
        lock (_sync) if (_items.TryGetValue(name, out var current) && ReferenceEquals(current, item)) _items.Remove(name);
    }

    public IReadOnlyList<ReadItemStatus> Status()
    {
        lock (_sync) return _items.Values.Select(i => new ReadItemStatus(i.Name, i.Enabled, i.Publications, i.LastSequence, i.Error, i.SkippedSamples)).ToArray();
    }

    private void Run()
    {
        try
        {
            while (true)
            {
                Item[] items;
                lock (_sync) { if (_closing) break; items = _items.Values.ToArray(); }
                int waitMs = 100;
                foreach (var item in items)
                {
                    InputSnapshot frame;
                    lock (_sync)
                    {
                        if (_closing) break;
                        if (!item.Enabled || _latest == null) continue;
                        long now = Stopwatch.GetTimestamp();
                        if (_periodic && now < item.NextDue)
                        {
                            waitMs = Math.Min(waitMs, Math.Max(1, (int)((item.NextDue - now) * 1000 / Stopwatch.Frequency)));
                            continue;
                        }
                        if (!_periodic && item.LastSequence == _latest.Sequence) continue;
                        frame = _latest;
                        item.Entered = true;
                        item.NextDue = now + (long)(item.IntervalMs * (double)Stopwatch.Frequency / 1000);
                    }
                    try
                    {
                        item.Publish(frame);
                        lock (_sync)
                        {
                            if (item.LastSequence >= 0 && frame.Sequence > item.LastSequence + 1) item.SkippedSamples += frame.Sequence - item.LastSequence - 1;
                            item.Publications++; item.LastSequence = frame.Sequence;
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (_sync) { item.Error = ex.ToString(); item.Enabled = false; }
                    }
                    finally
                    {
                        lock (_sync)
                        {
                            item.Entered = false;
                            if (!item.Enabled || _closing) item.Drained.TrySetResult();
                        }
                    }
                    if (_periodic) waitMs = Math.Min(waitMs, item.IntervalMs);
                }
                Action? maintenance;
                lock (_sync) maintenance = _closing ? null : _maintenance;
                maintenance?.Invoke(); // The channel callback isolates logging errors; never runs under _sync.
                _wake.WaitOne(waitMs);
            }
            _exited.TrySetResult();
        }
        catch (Exception ex) { _exited.TrySetException(ex); }
        finally
        {
            lock (_sync)
            {
                _closing = true;
                foreach (var item in _items.Values) { item.Enabled = false; item.Drained.TrySetResult(); }
            }
            // Keep the wait handle until no producer can Set it; Publish/Add are fenced by _closing.
            _wake.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_closing) return;
            _closing = true;
            foreach (var item in _items.Values) { item.Enabled = false; if (!item.Entered) item.Drained.TrySetResult(); }
            _wake.Set();
        }
    }
}
