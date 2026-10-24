using System.Diagnostics;
using CAN.Models;

namespace CAN.Adapters.NiXnet;

public sealed partial class NiXnetAdapter
{
    private readonly object _transmitGate = new();
    private readonly NiXnetIo _io;
    private readonly NiTransmitLedger _transmits = new();
    private readonly NiInterfaceClock _clock = new(Stopwatch.Frequency);
    private Guid _epoch = Guid.NewGuid();
    private volatile bool _closing;
    private long _lastCalibration;
    private string? _clockError;
    private bool _strictEnabled;
    private volatile bool _unknownSend;

    public NiXnetAdapter() => _io = new();
    // Driver injection without loading native code. Handles are owned exactly as after OpenInternal.
    internal NiXnetAdapter(NiXnetIo io, uint rx, uint tx)
    {
        _io = io; _rxSession = rx; _txSession = tx; _echoTxEnabled = true;
        _maxPendingFrames = 16384; _isConnected = true;
        _transmits.Reset(_epoch); StartReceivePump();
    }
    public bool ConfirmationAvailable => _isConnected && !_closing && _echoTxEnabled && !_unknownSend;
    public bool IsClockCurrent(string clockId) => _clock.IsCurrent(clockId, Stopwatch.GetTimestamp());

    public void VerifyConfirmationAvailable()
    {
        if (!Monitor.TryEnter(_transmitGate, 1000)) throw new TimeoutException("NI 原生发送占用中，确认预检未执行");
        try
        {
            if (!ConfirmationAvailable) throw new NotSupportedException($"NI 发送回显不可用：0x{_echoTxConfigStatus:X8}");
            ThrowIfReceivePumpFailed();
            ReadInterfaceTime();
            if (!RefreshTransmitCompletion()) throw new InvalidOperationException("旧发送尚未在总线上完成，不能切换严格模式");
            if (!_transmits.Quiescent) throw new InvalidOperationException("NI 仍有未确认发送，不允许启动新目标");
            _strictEnabled = true;
        }
        finally { Monitor.Exit(_transmitGate); }
    }

    public bool ConfirmTransmitQuiescent()
    {
        if (!Monitor.TryEnter(_transmitGate, 1000)) return false;
        try { return _strictEnabled && RefreshTransmitCompletion() && _transmits.Quiescent; }
        finally { Monitor.Exit(_transmitGate); }
    }

    private ulong ReadInterfaceTime()
    {
        _io.Check(_io.ReadTime(_rxSession, out var time, out var fault)); _io.Check(fault);
        if (time == 0) throw new InvalidOperationException("NI 接口时钟未启动");
        return time;
    }

    private bool RefreshTransmitCompletion()
    {
        var status = _io.TransmitComplete(_txSession);
        if (status == NiXnetApi.nxErrEventTimeout) return false;
        _io.Check(status);
        _transmits.SessionTransmitted();
        return true;
    }

    private NiTransmitLedger.Ticket? Submit(CanMessage message)
    {
        if (!_isConnected || _closing) throw new InvalidOperationException("CAN 通道未打开或正在关闭");
        if (_unknownSend) throw new InvalidOperationException("NI 旧发送曾超时/取消或失败，禁止新提交；保留未决确认，仅允许受控关闭");
        ThrowIfReceivePumpFailed();
        var frameBytes = BuildFrameBytes(message);
        NiTransmitLedger.Ticket? ticket = null;
        if (_strictEnabled)
        {
            RefreshTransmitCompletion();
            // Match the actual padded FD wire representation.
            ticket = _transmits.Register(ParseFrames(frameBytes, frameBytes.Length).Single(), ReadInterfaceTime());
        }
        var success = false;
        try { _io.Check(_io.Write(_txSession, frameBytes)); success = true; }
        catch { _unknownSend = true; throw; }
        finally { if (ticket != null) _transmits.NativeExited(ticket, success); }
        return ticket;
    }

    public void WriteConfirmed(CanMessage message, CancellationToken cancellationToken)
    {
        lock (_transmitGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ConfirmationAvailable) throw new NotSupportedException("NI 不支持发送完成回显");
            if (!_strictEnabled) VerifyConfirmationAvailable();
            var ticket = Submit(message)!;
            var deadline = Stopwatch.StartNew();
            try
            {
                while (true)
                {
                    RefreshTransmitCompletion();
                    if (_transmits.IsConfirmed(ticket)) return;
                    cancellationToken.ThrowIfCancellationRequested();
                    ThrowIfReceivePumpFailed();
                    if (deadline.ElapsedMilliseconds >= 5000)
                        throw new TimeoutException($"NI 发送确认超时，代次={ticket.Epoch}，顺序={ticket.Sequence}，ID=0x{message.Id:X}；未决项继续追踪");
                    Thread.Sleep(1);
                }
            }
            catch { _unknownSend = true; throw; }
        }
    }

    private void StopSession(uint session)
    {
        if (session == 0) return;
        _io.Check(_io.Stop(session));
        _io.Check(_io.State(session, out var state, out var fault)); _io.Check(fault);
        if ((state & 3) != 0) throw new InvalidOperationException("NI 会话尚未确认停止，禁止销毁句柄");
    }

    private void CalibrateClock(bool force)
    {
        var before = Stopwatch.GetTimestamp();
        if (!force && Stopwatch.GetElapsedTime(_lastCalibration, before).TotalMilliseconds < 5) return;
        _lastCalibration = before;
        try
        {
            var time = ReadInterfaceTime();
            _clock.Add(time, before, Stopwatch.GetTimestamp());
            _clockError = null;
        }
        catch (Exception ex) { _clock.Invalidate(); _clockError = ex.Message; }
    }
}
