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
    private bool _hasUntrackedSubmissions;

    public NiXnetAdapter() => _io = new NiXnetNativeIo();
    // Driver injection without loading native code. Handles are owned exactly as after OpenInternal.
    internal NiXnetAdapter(NiXnetIo io, uint rx, uint tx, bool orderedBatchAvailable = true)
    {
        _io = io; _rxSession = rx; _txSession = tx; _echoTxEnabled = true;
        _maxPendingFrames = 16384; _isConnected = true;
        _orderedBatchAvailable = orderedBatchAvailable; // Test fixture represents an already configured session.
        _transmits.Reset(_epoch); StartReceivePump();
    }
    public bool ConfirmationAvailable => _isConnected && !_closing && _echoTxEnabled && !_unknownSend;
    bool CAN.Runtime.ICanBudgetedConfirmationProbe.HasUnresolvedTransmit => _unknownSend || !_transmits.Quiescent;
    public bool IsClockCurrent(string clockId) => _clock.IsCurrent(clockId, Stopwatch.GetTimestamp());

    public void VerifyConfirmationAvailable() => VerifyConfirmationAvailable(CAN.Runtime.CanConfirmationBudget.Start(1000, default), default);

    void CAN.Runtime.ICanBudgetedConfirmationProbe.VerifyConfirmationAvailable(CAN.Runtime.CanConfirmationBudget budget, CancellationToken ct)
        => VerifyConfirmationAvailable(budget, ct);

    private void VerifyConfirmationAvailable(CAN.Runtime.CanConfirmationBudget budget, CancellationToken ct)
    {
        budget.Enter(_transmitGate, ct);
        try
        {
            if (!ConfirmationAvailable) throw new NotSupportedException($"NI 发送回显不可用：0x{_echoTxConfigStatus:X8}");
            ThrowIfReceivePumpFailed();
            ReadInterfaceTime();
            DrainPriorTransmits(budget, ct, !_strictEnabled || _hasUntrackedSubmissions, RefreshTransmitCompletion);
            _ = budget.Remaining(ct);
            _strictEnabled = true;
        }
        catch { if (!_transmits.Quiescent) _unknownSend = true; throw; }
        finally { Monitor.Exit(_transmitGate); }
    }

    private void DrainPriorTransmits(CAN.Runtime.CanConfirmationBudget budget, CancellationToken ct,
        bool untrackedQueue, Func<bool> refresh)
    {
        try
        {
            while (untrackedQueue || !_transmits.Quiescent)
            {
                long version = _transmits.Version;
                _ = budget.Remaining(ct); ThrowIfReceivePumpFailed();
                if (untrackedQueue || _transmits.PendingReadyForSessionQuery)
                {
                    bool completed = refresh();
                    if (completed) untrackedQueue = false;
                    _ = budget.Remaining(ct);
                }
                if (untrackedQueue || !_transmits.Quiescent)
                    _transmits.WaitForChange(version, Math.Min(2, budget.Remaining(ct)));
            }
        }
        catch { if (untrackedQueue || !_transmits.Quiescent) _unknownSend = true; throw; }
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
        _transmits.SessionTransmitted(); _hasUntrackedSubmissions = false;
        return true;
    }

    private NiTransmitLedger.Ticket? Submit(CanMessage message, Action? checkBeforeNative = null)
    {
        if (!_isConnected || _closing) throw new InvalidOperationException("CAN 通道未打开或正在关闭");
        if (_unknownSend) throw new InvalidOperationException("NI 旧发送曾超时/取消或失败，禁止新提交；保留未决确认，仅允许受控关闭");
        ThrowIfReceivePumpFailed();
        var frameBytes = BuildFrameBytes(message);
        NiTransmitLedger.Ticket? ticket = null;
        if (_strictEnabled)
        {
            if (_transmits.PendingReadyForSessionQuery) RefreshTransmitCompletion();
            // Match the actual padded FD wire representation.
            var barrier = ReadInterfaceTime();
            checkBeforeNative?.Invoke();
            ticket = _transmits.Register(ParseFrames(frameBytes, frameBytes.Length).Single(), barrier);
        }
        else checkBeforeNative?.Invoke();
        var success = false;
        try { _io.Check(_io.Write(_txSession, frameBytes)); success = true; }
        catch { _unknownSend = true; throw; }
        finally { if (ticket != null) _transmits.NativeExited(ticket, success); }
        return ticket;
    }

    public void WriteConfirmed(CanMessage message, CancellationToken cancellationToken)
        => WriteConfirmedCore(message, CAN.Runtime.CanConfirmationBudget.Start(5000, cancellationToken), cancellationToken);

    void CAN.Runtime.ICanBudgetedConfirmedTransmitter.WriteConfirmed(CanMessage message, CAN.Runtime.CanConfirmationBudget budget, CancellationToken cancellationToken)
        => WriteConfirmedCore(message, budget, cancellationToken);

    private void WriteConfirmedCore(CanMessage message, CAN.Runtime.CanConfirmationBudget budget, CancellationToken cancellationToken)
    {
        _ = budget.Remaining(cancellationToken);
        lock (_transmitGate)
        {
            _ = budget.Remaining(cancellationToken);
            if (!ConfirmationAvailable) throw new NotSupportedException("NI 不支持发送完成回显");
            if (!_strictEnabled || _hasUntrackedSubmissions) VerifyConfirmationAvailable(budget, cancellationToken);
            var ticket = Submit(message, () => _ = budget.Remaining(cancellationToken))!;
            try
            {
                while (true)
                {
                    _ = budget.Remaining(cancellationToken);
                    RefreshTransmitCompletion();
                    _ = budget.Remaining(cancellationToken);
                    if (_transmits.IsConfirmed(ticket)) return;
                    ThrowIfReceivePumpFailed();
                    Thread.Sleep(1);
                }
            }
            catch (TimeoutException ex)
            {
                _unknownSend = true;
                throw new TimeoutException($"NI 发送确认超时，代次={ticket.Epoch}，顺序={ticket.Sequence}，ID=0x{message.Id:X}，配置总期限={budget.TimeoutMs} ms；未决项继续追踪", ex);
            }
            catch { _unknownSend = true; throw; }
        }
    }

    private void RequestSessionStop(uint session)
    {
        if (session == 0) return;
        _io.Check(_io.Stop(session));
        // Normal can return before this session stops when another session is
        // running. NI permits nxClear on a started session: Clear itself stops
        // communication and releases its resources. A one-shot SessionInfo read
        // is not a legal precondition for Clear (nor proof of host I/O exit).
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
