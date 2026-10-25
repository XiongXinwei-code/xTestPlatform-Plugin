using System.Diagnostics;
using CAN.Models;
using CAN.Runtime;

namespace CAN.Adapters.Zlg;

public sealed partial class ZlgAdapter
{
    private sealed class Pending(Guid epoch, long sequence, CanMessage frame, long before, ulong echoFloor)
    {
        public readonly Guid Epoch = epoch;
        public readonly long Sequence = sequence;
        public readonly CanMessage Frame = frame;
        public readonly long HostBefore = before;
        public readonly ulong EchoFloor = echoFloor;
        public bool NativeDone, NativeSuccess;
        public ulong? EchoTimestamp;
        public readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly ZlgIo _io;
    private readonly object _runtimeGate = new(), _transmitGate = new(), _closeGate = new();
    private readonly Queue<CanMessage> _received = new();
    private readonly Queue<Pending> _pending = new();
    private readonly HashSet<ulong> _seenEchoTimes = [];
    private readonly Queue<ulong> _echoHistory = new();
    private readonly ZlgEchoClock _clock = new();
    private Guid _epoch = Guid.NewGuid();
    private CancellationTokenSource? _receiveStop;
    private Task? _receivePump;
    private volatile Exception? _pumpError;
    private volatile bool _closing, _unknownSend;
    private bool _echoSupported;
    private long _sendSequence, _managedDropped;
    private ulong _lastEcho;

    public ZlgAdapter() => _io = new();
    internal ZlgAdapter(ZlgIo io, uint type, bool fd)
    {
        _io = io; _deviceHandle = new(1); _channelHandle = new(2); _isFd = fd; _isConnected = true;
        StartRuntime(type, 0);
    }

    public bool ConfirmationAvailable => _isConnected && !_closing && _echoSupported && !_unknownSend && _pumpError == null;
    public bool HandlesReleased => _deviceHandle == IntPtr.Zero && _channelHandle == IntPtr.Zero && _receivePump == null;
    public bool IsClockCurrent(string clockId)
    { lock (_runtimeGate) return _clock.IsCurrent(clockId, Stopwatch.GetTimestamp()); }

    private void StartRuntime(uint type, uint channel)
    {
        lock (_runtimeGate)
        {
            if (_pending.Count != 0 || _receivePump != null) throw new InvalidOperationException("ZLG 旧接收泵/未决发送尚未释放");
            _sendSequence = 0; _lastEcho = 0; _managedDropped = 0;
            _seenEchoTimes.Clear(); _echoHistory.Clear(); _received.Clear();
            _clock.Reset(); _epoch = Guid.NewGuid();
        }
        _closing = _unknownSend = false; _pumpError = null;
        // Only these model XMLs and the bundled driver advertise set_device_tx_echo.
        // The generic header flag alone is not proof every ZLG product supports it.
        _echoSupported = (type is ZlgApi.ZCAN_USBCANFD_100U or ZlgApi.ZCAN_USBCANFD_200U) &&
            _io.SetProperty(_deviceHandle, $"{channel}/set_device_tx_echo", "1");
        if (_echoSupported && _io.ClearBuffer(_channelHandle) != ZlgApi.STATUS_OK)
            throw new InvalidOperationException("ZLG 无法清空旧代次接收/回显队列，不能开始新连接");
        _receiveStop = new(); var token = _receiveStop.Token; var epoch = _epoch;
        _receivePump = Task.Run(() => ReceivePumpAsync(epoch, token));
        _ = _receivePump.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void VerifyConfirmationAvailable()
    {
        if (!Monitor.TryEnter(_transmitGate, 1000)) throw new TimeoutException("ZLG 旧发送尚未退出，确认预检未执行");
        try
        {
            if (!ConfirmationAvailable)
                throw new NotSupportedException("UnsupportedGuarantee：该 ZLG 型号/驱动未提供已核对的总线发送回显，或连接已故障");
            lock (_runtimeGate)
            {
                ThrowIfPumpFailed();
                if (_pending.Count != 0) throw new InvalidOperationException("ZLG 旧发送尚未回显确认，不能开始新运动");
            }
        }
        finally { Monitor.Exit(_transmitGate); }
    }

    public bool ConfirmTransmitQuiescent()
    {
        if (!Monitor.TryEnter(_transmitGate)) return false;
        try { lock (_runtimeGate) return _echoSupported && _pending.Count == 0; }
        finally { Monitor.Exit(_transmitGate); }
    }

    private Pending? Submit(CanMessage message, Action? checkBeforeNative = null)
    {
        if (!_isConnected || _closing) throw new InvalidOperationException("ZLG 未连接或正在关闭");
        if (_unknownSend) throw new InvalidOperationException("ZLG 存在超时/取消或失败发送，禁止新提交；仅允许确认收尾后关闭");
        if (message.Data == null || message.Id > (message.FrameType == CanFrameType.Standard ? 0x7FFu : ZlgApi.CAN_ID_MASK) ||
            message.Data.Length > (message.IsFd ? 64 : 8) || (message.IsFd && !_isFd))
            throw new ArgumentException("ZLG 报文 ID、载荷长度或 CAN FD 协议无效");
        var wire = CanReceiveRouter.Copy(message);
        if (wire.IsFd && wire.Data.Length != ToFdLength(wire.Data.Length))
        {
            var padded = new byte[ToFdLength(wire.Data.Length)]; wire.Data.CopyTo(padded, 0); wire.Data = padded;
        }
        Pending? ticket = null;
        lock (_runtimeGate)
        {
            ThrowIfPumpFailed();
            checkBeforeNative?.Invoke();
            if (_echoSupported)
            {
                if (_pending.Count >= 8192) throw new InvalidOperationException("ZLG 未决发送达到上限，旧发送尚未确认");
                ticket = new(_epoch, ++_sendSequence, wire, Stopwatch.GetTimestamp(), _lastEcho);
                _pending.Enqueue(ticket);
            }
        }
        var success = false;
        try { Transmit(wire); success = true; }
        catch { _unknownSend = true; throw; }
        finally
        {
            if (ticket != null)
                lock (_runtimeGate)
                {
                    ticket.NativeDone = true; ticket.NativeSuccess = success; RetireConfirmed();
                }
        }
        return ticket;
    }

    public void WriteConfirmed(CanMessage message, CancellationToken cancellationToken)
        => WriteConfirmedCore(message, CanConfirmationBudget.Start(5000, cancellationToken), cancellationToken);

    void ICanBudgetedConfirmedTransmitter.WriteConfirmed(CanMessage message, CanConfirmationBudget budget, CancellationToken cancellationToken)
        => WriteConfirmedCore(message, budget, cancellationToken);

    private void WriteConfirmedCore(CanMessage message, CanConfirmationBudget budget, CancellationToken cancellationToken)
    {
        _ = budget.Remaining(cancellationToken);
        lock (_transmitGate)
        {
            _ = budget.Remaining(cancellationToken); VerifyConfirmationAvailable();
            var ticket = Submit(message, () => _ = budget.Remaining(cancellationToken))!;
            try
            {
                ticket.Completion.Task.WaitAsync(TimeSpan.FromMilliseconds(budget.Remaining(cancellationToken)), cancellationToken).GetAwaiter().GetResult();
                _ = budget.Remaining(cancellationToken);
            }
            catch (TimeoutException ex)
            {
                _unknownSend = true;
                throw new TimeoutException($"ZLG 等待总线回显超时；代次={ticket.Epoch}；发送序号={ticket.Sequence}；ID=0x{ticket.Frame.Id:X}；DLC={ticket.Frame.Dlc}；票据保留，仅允许确认收尾后关闭", ex);
            }
            catch { _unknownSend = true; throw; }
        }
    }

    private void RetireConfirmed()
    {
        while (_pending.TryPeek(out var head) && head.NativeDone && head.NativeSuccess && head.EchoTimestamp is ulong time)
        {
            _pending.Dequeue(); _lastEcho = Math.Max(_lastEcho, time);
            _clock.Add(time, head.HostBefore, Stopwatch.GetTimestamp()); head.Completion.TrySetResult();
        }
        Monitor.PulseAll(_runtimeGate);
    }

    private void AcceptFrame(Guid epoch, uint canId, byte[] data, int length, bool fd, byte flags, ulong timestamp)
    {
        if ((canId & ZlgApi.CAN_ERR_FLAG) != 0) throw new InvalidOperationException($"ZLG 接收到总线错误帧：0x{canId:X8}");
        if ((canId & ZlgApi.CAN_RTR_FLAG) != 0) return;
        if (length > (fd ? 64 : 8) || (fd && length != ToFdLength(length)) || data == null || data.Length < length)
            throw new InvalidOperationException($"ZLG 返回无效 DLC/缓冲区：{length}");
        var message = new CanMessage
        {
            Id = canId & ZlgApi.CAN_ID_MASK,
            FrameType = (canId & ZlgApi.CAN_EFF_FLAG) != 0 ? CanFrameType.Extended : CanFrameType.Standard,
            Data = data.AsSpan(0, length).ToArray(),
            IsFd = fd,
            RawTimestampUs = timestamp,
            TimestampNs = timestamp <= long.MaxValue / 1000 ? (long)timestamp * 1000 : 0,
            IsTransmitEcho = (flags & ZlgApi.TX_ECHO_FLAG) != 0,
            IngressMonotonicTicks = Stopwatch.GetTimestamp()
        };
        lock (_runtimeGate)
        {
            if (epoch != _epoch) return;
            if (message.IsTransmitEcho)
            {
                // CAN and FD have independent native receive queues. Match the first
                // identical outstanding frame, not only the global submission head.
                var ticket = !_seenEchoTimes.Contains(timestamp) ? _pending.FirstOrDefault(p => p.Epoch == epoch &&
                    p.EchoTimestamp == null && timestamp > p.EchoFloor && p.Frame.Id == message.Id &&
                    p.Frame.FrameType == message.FrameType && p.Frame.IsFd == fd && p.Frame.Data.AsSpan().SequenceEqual(message.Data)) : null;
                if (ticket != null)
                {
                    ticket.EchoTimestamp = timestamp; _seenEchoTimes.Add(timestamp); _echoHistory.Enqueue(timestamp);
                    if (_echoHistory.Count > 16384) _seenEchoTimes.Remove(_echoHistory.Dequeue());
                    RetireConfirmed();
                }
                return;
            }
            message.TimestampDomain = "ZLG.DeviceMicroseconds"; message.ClockId = _clock.Id;
            message.CaptureMonotonicLowerBoundTicks = _clock.LowerBound(timestamp, Stopwatch.GetTimestamp());
            if (_received.Count == 16384) { _received.Dequeue(); _managedDropped++; }
            _received.Enqueue(message); Monitor.PulseAll(_runtimeGate);
        }
    }

    private async Task ReceivePumpAsync(Guid epoch, CancellationToken ct)
    {
        var classic = Enumerable.Range(0, 64).Select(_ => new ZlgApi.ZCAN_Receive_Data { frame = new() { data = new byte[8] } }).ToArray();
        var fd = Enumerable.Range(0, 64).Select(_ => new ZlgApi.ZCAN_ReceiveFD_Data { frame = new() { data = new byte[64] } }).ToArray();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var count = _io.Receive(_channelHandle, classic, 0);
                if (count > classic.Length) throw new InvalidOperationException($"ZLG Classic 接收失败：0x{count:X8}");
                for (var i = 0; i < count; i++) AcceptFrame(epoch, classic[i].frame.can_id, classic[i].frame.data,
                    classic[i].frame.can_dlc, false, classic[i].frame.pad, classic[i].timestamp);
                var fdCount = _isFd ? _io.ReceiveFd(_channelHandle, fd, 0) : 0;
                if (fdCount > fd.Length) throw new InvalidOperationException($"ZLG CAN FD 接收失败：0x{fdCount:X8}");
                for (var i = 0; i < fdCount; i++) AcceptFrame(epoch, fd[i].frame.can_id, fd[i].frame.data,
                    fd[i].frame.len, true, fd[i].frame.flags, fd[i].timestamp);
                if (count == 0 && fdCount == 0) await Task.Delay(1, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_runtimeGate) { _pumpError = ex; Monitor.PulseAll(_runtimeGate); }
            throw;
        }
    }

    private void ThrowIfPumpFailed()
    { if (_pumpError != null) throw new InvalidOperationException("ZLG 后台接收失败", _pumpError); }

    public CanQueueStatistics GetQueueStatistics()
    { lock (_runtimeGate) return new(null, null, _received.Count, _managedDropped); }

    private void CloseRuntime()
    {
        lock (_closeGate)
        {
            if (HandlesReleased) return;
            _closing = true;
            if (!Monitor.TryEnter(_transmitGate, 1000)) throw new TimeoutException("ZLG 原生发送/确认尚未退出，保留任务与句柄");
            try
            {
                var elapsed = Stopwatch.StartNew();
                lock (_runtimeGate)
                {
                    while (_pending.Count != 0)
                    {
                        if (elapsed.ElapsedMilliseconds >= 1000) throw new TimeoutException("ZLG 未决发送尚未回显确认，保留接收泵及句柄");
                        Monitor.Wait(_runtimeGate, 10);
                    }
                }
                _receiveStop?.Cancel();
                if (_receivePump != null)
                {
                    try { _receivePump.WaitAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult(); }
                    catch (TimeoutException) when (!_receivePump.IsCompleted) { throw new TimeoutException("ZLG 接收泵尚未实际退出，未 Reset/Close 句柄"); }
                    catch (Exception) when (_receivePump.IsFaulted) { /* Report stored original after releasing safe handles. */ }
                }
                if (_channelHandle != IntPtr.Zero)
                {
                    if (_io.ResetCan(_channelHandle) != ZlgApi.STATUS_OK) throw new InvalidOperationException("ZLG ResetCAN 失败，保留通道/设备句柄");
                    _channelHandle = IntPtr.Zero;
                }
                if (_deviceHandle != IntPtr.Zero)
                {
                    if (_io.CloseDevice(_deviceHandle) != ZlgApi.STATUS_OK) throw new InvalidOperationException("ZLG CloseDevice 失败，保留设备句柄");
                    _deviceHandle = IntPtr.Zero;
                }
                _receivePump = null; _receiveStop?.Dispose(); _receiveStop = null;
                _isConnected = false;
                lock (_runtimeGate) { _clock.Reset(); _received.Clear(); Monitor.PulseAll(_runtimeGate); }
                if (_pumpError != null) throw new InvalidOperationException("ZLG 句柄已关闭，但后台接收曾失败", _pumpError);
            }
            finally { Monitor.Exit(_transmitGate); }
        }
    }
}
