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
    }
    private readonly object _gate = new();
    private readonly Queue<Ticket> _pending = new();
    private Guid _epoch;
    private long _sequence;
    private ulong _lastEcho;
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
    {
        lock (_gate)
        {
            if (_pending.Count >= 8192) throw new InvalidOperationException("NI 发送确认积压达到上限，禁止继续提交");
            var ticket = new Ticket(_epoch, ++_sequence, CAN.Runtime.CanReceiveRouter.Copy(frame), barrier);
            _pending.Enqueue(ticket);
            return ticket;
        }
    }
    public void NativeExited(Ticket ticket, bool success)
    { lock (_gate) { ticket.NativeDone = true; ticket.NativeSuccess = success; } }
    public bool IsConfirmed(Ticket ticket) { lock (_gate) return ticket.Confirmed; }
    public void Observe(Guid epoch, CanMessage echo)
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
            _lastEcho = time;
        }
    }
    // Called with the native transmit gate held. nxWait covers this session, not other interface users.
    public void SessionTransmitted()
    {
        lock (_gate)
            while (_pending.TryPeek(out var ticket) && ticket.EchoSeen && ticket.NativeDone && ticket.NativeSuccess)
            { ticket.Confirmed = true; _pending.Dequeue(); }
    }
}
