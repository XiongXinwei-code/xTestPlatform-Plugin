using CAN.Models;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CAN.ContractTests")]
namespace CAN.Adapters.NiXnet;

// Ticket lifetime belongs to the connection, never to a cancelled waiter.
internal sealed class NiTransmitLedger
{
    internal sealed class Ticket(Guid epoch, long sequence, CanMessage frame, ulong barrier)
    {
        public Guid Epoch { get; } = epoch;
        public long Sequence { get; } = sequence;
        public CanMessage Frame { get; } = frame;
        public ulong Barrier { get; } = barrier;
        public bool EchoSeen { get; set; }
        public bool NativeDone { get; set; }
        public bool NativeSuccess { get; set; }
        public bool Confirmed { get; set; }
        public Guid BatchId { get; init; }
        public int BatchIndex { get; init; }
        public ulong? EchoTimestamp { get; set; }
        public long? EchoReadReturned { get; set; }
        public long? EchoObserved { get; set; }
    }
    private readonly object _gate = new();
    private readonly Queue<Ticket> _pending = new();
    private Guid _epoch;
    private long _sequence;
    private ulong _lastEcho;
    private long _version;
    public long Version { get { lock (_gate) return _version; } }
    public void Signal() { lock (_gate) { _version++; Monitor.PulseAll(_gate); } }
    public void WaitForChange(long version, int timeoutMs)
    { lock (_gate) if (_version == version) Monitor.Wait(_gate, timeoutMs); }
    public bool Quiescent { get { lock (_gate) return _pending.Count == 0; } }
    public int PendingCount { get { lock (_gate) return _pending.Count; } }
    public void Reset(Guid epoch)
    {
        lock (_gate)
        {
            if (_pending.Count != 0) throw new InvalidOperationException("未决 NI 发送不能跨连接代次清除");
            _epoch = epoch; _sequence = 0; _lastEcho = 0;
        }
    }
    public Ticket Register(CanMessage frame, ulong barrier)
        => RegisterBatch([frame], barrier, Guid.Empty)[0];

    public Ticket[] RegisterBatch(CanMessage[] frames, ulong barrier, Guid batchId)
    {
        var copies = frames.Select(CAN.Runtime.CanReceiveRouter.Copy).ToArray();
        lock (_gate)
        {
            if (copies.Length == 0 || copies.Length > 8192 - _pending.Count)
                throw new InvalidOperationException("NI 发送确认整批容量不足，未登记或提交任何本批帧");
            var tickets = new Ticket[copies.Length];
            for (int i = 0; i < copies.Length; i++)
                tickets[i] = new(_epoch, checked(_sequence + i + 1), copies[i], barrier) { BatchId = batchId, BatchIndex = i };
            foreach (var ticket in tickets) _pending.Enqueue(ticket);
            _sequence += copies.Length; Signal(); return tickets;
        }
    }
    public void RollbackUnsubmitted(Ticket[] tickets)
    {
        lock (_gate)
        {
            if (tickets.Any(t => t.NativeDone)) throw new InvalidOperationException("已进入原生调用的票据不能回滚");
            var removed = tickets.ToHashSet();
            var retained = _pending.Where(t => !removed.Contains(t)).ToArray();
            _pending.Clear(); foreach (var ticket in retained) _pending.Enqueue(ticket);
            Signal();
        }
    }
    public void NativeExited(Ticket ticket, bool success)
    { lock (_gate) { ticket.NativeDone = true; ticket.NativeSuccess = success; Signal(); } }
    public bool IsConfirmed(Ticket ticket) { lock (_gate) return ticket.Confirmed; }
    public bool ReadyForSessionQuery(Ticket[] tickets)
    {
        lock (_gate) return tickets.Length != 0 && tickets.All(ticket => ticket.Epoch == _epoch &&
            ticket.EchoSeen && ticket.NativeDone && ticket.NativeSuccess);
    }
    public bool PendingReadyForSessionQuery
    {
        get { lock (_gate) return _pending.Count != 0 && _pending.All(ticket => ticket.Epoch == _epoch &&
            ticket.EchoSeen && ticket.NativeDone && ticket.NativeSuccess); }
    }
    public void Observe(Guid epoch, CanMessage echo, long? readReturned = null)
    {
        lock (_gate)
        {
            if (epoch != _epoch || !echo.IsTransmitEcho || echo.RawTimestamp100ns is not ulong time || time <= _lastEcho) return;
            var ticket = _pending.FirstOrDefault(t => !t.EchoSeen);
            if (ticket == null || time <= ticket.Barrier) return;
            var expected = ticket.Frame;
            if (expected.Id != echo.Id || expected.FrameType != echo.FrameType || expected.IsFd != echo.IsFd ||
                !expected.Data.AsSpan().SequenceEqual(echo.Data)) return;
            ticket.EchoSeen = true;
            ticket.EchoTimestamp = time; ticket.EchoReadReturned = readReturned;
            ticket.EchoObserved = System.Diagnostics.Stopwatch.GetTimestamp();
            _lastEcho = time;
            Signal();
        }
    }
    // Called with the native transmit gate held. nxWait covers this session, not other interface users.
    public void SessionTransmitted()
    {
        lock (_gate)
        {
            while (_pending.TryPeek(out var ticket) && ticket.EchoSeen && ticket.NativeDone && ticket.NativeSuccess)
            { ticket.Confirmed = true; _pending.Dequeue(); }
        }
    }

    public CAN.Runtime.CanBatchFrameEvidence[] Evidence(Ticket[] tickets)
    {
        lock (_gate) return tickets.Select(t => new CAN.Runtime.CanBatchFrameEvidence(t.BatchIndex, t.Frame.Id,
            t.Frame.FrameType.ToString(), t.Frame.IsFd, t.Frame.Dlc, Convert.ToHexString(t.Frame.Data), t.Sequence, t.Barrier,
            t.NativeDone, t.NativeDone ? t.NativeSuccess : null, t.Confirmed, t.EchoTimestamp, t.EchoReadReturned, t.EchoObserved)).ToArray();
    }
}
