using System.Diagnostics;
using System.Collections.Concurrent;
using xTestPlatform.Core.Engine;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;
using ZlgEtherCat.Motion;
using ZlgEtherCat.Native;

namespace ZlgEtherCat.Runtime;

/// <summary>一个物理通道的原生调用、映像及两个读取功能池的唯一所有者。</summary>
internal sealed class ZlgChannel : IDisposable, IZlgMotionIo
{
    private readonly IZlgNativeApi _api;
    private readonly SdkWorker _worker;
    private readonly object _state = new();
    private readonly Action _releaseReservation;
    private readonly Action<Exception> _report;
    private readonly Func<IDisposable?> _boardMaintenance;
    private readonly uint _queueCapacity;
    private readonly Action<RxQueueNotice>? _warning;
    private RxQueueMonitor _rx = new();
    private readonly ZlgSdkOperationState _sdkState = new();
    private readonly HashSet<(bool Pwm, uint Port)> _activeBoardOutputs = new();
    private IDisposable? _boardOutputLease;
    private bool _boardLatchOwned, _boardLatchNeedsStop, _boardLatchNeedsDisable;
    private readonly Queue<MotionFrame> _motionFrames = new();
    private readonly AsyncLocal<string?> _motionOperation = new();
    private sealed record MotionFrame(string Axis, MotionPdoWrite[] Writes, bool RenewLease, bool StopLease, CancellationToken Token, TaskCompletionSource Receipt);
    private sealed class UnsubmittedMotionException(string message) : InvalidOperationException(message);
    private readonly ReadFunctionPool _background = new(false), _periodic = new(true);
    private readonly Dictionary<string, (bool Periodic, int MaxItems, HashSet<string> Items)> _pools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MotionAxisConfiguration> _claims = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _leases = new(StringComparer.Ordinal);
    private readonly Queue<InputSnapshot> _history = new();
    private readonly Dictionary<ushort, (PiRange Input, PiRange Output)> _ranges = new();
    private readonly ConcurrentDictionary<string, ZlgMotionAxis> _axes = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _operations = new(1, 1);
    private IntPtr _handle;
    private BoardDetails _board = new("", "", "", 0, 0, 0);
    private EniDetails? _eni;
    private EniProcessImageLayout? _layout;
    private ExchangeMode _mode;
    private byte[] _output = [];
    private InputSnapshot? _snapshot;
    private long _sequence, _lastOutputCommand, _nextOutput, _historyDropped;
    private int _historyCapacity = 1024, _nativeWaitMs = 1, _outputIntervalMs, _commandLeaseMs;
    private volatile bool _acquiring, _closing;
    private volatile bool _exitRequested;
    private bool _latestOnly, _outputRunning, _configured, _requireDc, _pendingOutput;
    private Task? _closeTask;
    private string? _error;

    internal ZlgChannel(IZlgNativeApi api, string name, Action releaseReservation, Action<Exception> report, Func<IDisposable?>? boardMaintenance = null, uint queueCapacity = 1,
        Action<RxQueueNotice>? warning = null, Func<string, SdkWorker>? workerFactory = null)
    {
        _api = api; _releaseReservation = releaseReservation; _report = report;
        _boardMaintenance = boardMaintenance ?? (() => null);
        _queueCapacity = queueCapacity;
        _warning = warning;
        _worker = workerFactory?.Invoke(name) ?? new SdkWorker(name);
        _worker.Tick = Tick;
        _worker.BackgroundError = ex =>
        {
            _acquiring = false; _outputRunning = false; Volatile.Write(ref _snapshot, null);
            Volatile.Read(ref _rx).Stop();
            try { _worker.RestoreAcquisitionPriority(); }
            catch (Exception restore) { ex = new AggregateException(ex, restore); }
            lock (_state) _error = ex.ToString();
            while (_motionFrames.TryDequeue(out var frame)) frame.Receipt.TrySetException(ex);
            Report(ex);
        };
        _background.SetMaintenance(DeliverRxNotice);
    }
    internal Task OpenAsync(string selector, uint channel, int timeout, CancellationToken ct) =>
        _worker.InvokeAsync(() => { if (_closing) throw new InvalidOperationException("打开请求所属通道已退出"); _handle = _api.Open(selector, channel); _api.SetTimeout(_handle, (uint)timeout); _board = _api.GetBoardInfo(_handle); }, timeout, ct);
    private void Check()
    {
        if (_closing) throw new InvalidOperationException("通道正在退出");
        if (_handle == IntPtr.Zero) throw new InvalidOperationException("通道尚未打开");
        if (_worker.IsolatedReason is { } reason) throw new InvalidOperationException(reason);
    }
    private void RequireNoAxes()
    {
        lock (_state) if (_claims.Count != 0) throw new InvalidOperationException("请先停止、去使能并解绑所有轴，再改变通道运行状态");
    }
    private void RequireNoBoardOutputs()
    {
        lock (_state) if (_activeBoardOutputs.Count != 0)
            throw new InvalidOperationException("板载 PWM/脉冲输出尚未明确停止；请先执行对应 StopBoardPwm/StopBoardPulse，再关闭通道。关闭 EtherCAT 通道不能证明板载输出已停止。");
    }
    private void RequireStoppedExchange()
    {
        RequireNoAxes();
        if (_pools.Count != 0) throw new InvalidOperationException("请先关闭读取功能池，再改变交换配置");
        if (_acquiring || _outputRunning || _api.IsPiEnabled(_handle)) throw new InvalidOperationException("该配置要求停止采集、输出和过程映像交换");
    }
    private void RequireOp()
    {
        if (_api.GetMasterState(_handle) != 8) throw new InvalidOperationException("主站尚未确认 OP");
        if (_requireDc)
        {
            var state = _api.ReadDiagnostics(_handle, []).State;
            if ((state & 0xA01) != 0xA01) throw new InvalidOperationException($"DC 内部同步/传播延迟初始化尚未建立，诊断=0x{state:X}");
        }
    }
    private (PiRange Input, PiRange Output) Range(ushort slave) => _ranges.TryGetValue(slave, out var range) ? range : throw new ArgumentException($"从站 {slave} 不存在或未加载 ENI");
    private static int Absolute(PiRange range, int offset, int length)
    {
        if (offset < 0 || length <= 0 || (long)offset + length > range.Length || (long)range.Offset + offset + length > int.MaxValue) throw new ArgumentException("PDO 字节范围超出从站映像");
        return checked((int)range.Offset + offset);
    }
    private void RejectClaim(ushort slave)
    {
        lock (_state) if (_claims.Values.Any(c => c.SlaveIndex == slave)) throw new InvalidOperationException("从站已由运动轴独占，不能通过原始写入绕过轴状态机");
    }

    internal async Task<object?> ExecuteAsync(string op, ZlgRequest r, IExecutionContext context, CancellationToken ct)
    {
        using var operationDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        operationDeadline.CancelAfter(r.TimeoutMs);
        try { return await ExecuteCoreAsync(op, r, context, operationDeadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && operationDeadline.IsCancellationRequested)
        { throw new TimeoutException($"ZLG 通道步骤超过 {r.TimeoutMs} ms，未确认的原生调用仍保留归属", ex); }
    }
    private void CheckExitOperation(string op, ZlgRequest r)
    {
        if (!_exitRequested || op == "BeginExit" || op == "Close" || op.StartsWith("Read", StringComparison.Ordinal) || op.StartsWith("Stop", StringComparison.Ordinal)
            || op.StartsWith("Disable", StringComparison.Ordinal) || op is "UnbindAxis" or "WaitNextPdo" or "EnumerateSlaves"
            || op.EndsWith("PoolStopItem", StringComparison.Ordinal) || op.EndsWith("PoolClose", StringComparison.Ordinal) || op.EndsWith("PoolStatus", StringComparison.Ordinal)
            || op == "RequestState" && r.Get<MasterState>("State") != MasterState.Operational) return;
        throw new InvalidOperationException("通道已进入退出流程，拒绝新业务写入、使能、运动和读取项配置；请完成停止及关闭后重新打开");
    }
    private async Task<object?> ExecuteCoreAsync(string op, ZlgRequest r, IExecutionContext context, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        int Left() { ct.ThrowIfCancellationRequested(); int left = r.TimeoutMs - (int)clock.ElapsedMilliseconds; return left > 0 ? left : throw new TimeoutException("通道操作总期限已到"); }
        ct.ThrowIfCancellationRequested();
        if (op == "BeginExit") { _exitRequested = true; return new { ExitRequested = true, PhysicalStopConfirmed = false }; }
        CheckExitOperation(op, r);
        if (op == "ReadAcquisitionStatus")
        {
            // Cached diagnostics must remain available while an SDK call is blocked or isolated.
            if (_closing || _handle == IntPtr.Zero) throw new InvalidOperationException("通道未打开或正在退出");
            return AcquisitionStatus();
        }
        if (op == "Close")
        {
            // Closing must remain accessible after a soft timeout quarantines the channel.
            if (_worker.IsolatedReason == null) { RequireNoAxes(); RequireNoBoardOutputs(); }
            await CloseAsync().WaitAsync(TimeSpan.FromMilliseconds(Left()), ct).ConfigureAwait(false);
            return new { Closed = true, PhysicalStopConfirmed = false, Detail = "通道句柄已退出；驱动停稳须由退出前的运动停止及反馈步骤确认" };
        }
        if (ZlgMotionOperations.Handles(op) && op is not ("BindAxis" or "UnbindAxis"))
        {
            Check();
            _motionOperation.Value = op;
            try { return await ZlgMotionOperations.ExecuteAsync(op, r with { TimeoutMs = Left() }, this, _axes, ct).ConfigureAwait(false); }
            finally { _motionOperation.Value = null; }
        }
        if (!await _operations.WaitAsync(Left(), ct).ConfigureAwait(false)) throw new TimeoutException("通道步骤排队超时");
        try
        {
            Check();
            CheckExitOperation(op, r);
            if (op.Contains("Pool", StringComparison.Ordinal)) return await PoolAsync(op, r, context, ct).ConfigureAwait(false);
            if (ZlgMotionOperations.Handles(op))
            {
                try { return await ZlgMotionOperations.ExecuteAsync(op, r with { TimeoutMs = Left() }, this, _axes, ct).ConfigureAwait(false); }
                catch
                {
                    if (op == "BindAxis") lock (_state)
                    {
                        string name = r.Get<string>("AxisName");
                        if (_claims.ContainsKey(name) && !_axes.ContainsKey(name)) _worker.Isolate("绑定失败且轴访问尚未确认退出，保留归属并允许通道退出清理");
                    }
                    throw;
                }
            }
            if (op == "RequestState")
            {
                byte state = (byte)r.Get<MasterState>("State");
                await _worker.InvokeAsync(() => { Check(); CheckExitOperation(op, r); if (state != 8) RequireNoAxes(); _api.RequestMasterState(_handle, state); }, Left(), ct).ConfigureAwait(false);
                while (true)
                {
                    bool reached = await _worker.InvokeAsync(() =>
                    {
                        if (_api.GetMasterState(_handle) != state) return false;
                        if (state == 8 && _requireDc && (_api.ReadDiagnostics(_handle, []).State & 0xA01) != 0xA01) return false;
                        return true;
                    }, Left(), ct).ConfigureAwait(false);
                    if (reached) return state;
                    await Task.Delay(Math.Min(r.Get<int>("PollIntervalMs"), Left()), ct).ConfigureAwait(false);
                }
            }
            if (op == "WaitNextPdo")
            {
                if (_mode != ExchangeMode.Queue) throw new InvalidOperationException("等待新 PDO 帧要求队列交换模式；异步映像不提供帧到达证据");
                long after = r.Get<long>("AfterSequence");
                while (true)
                {
                    ct.ThrowIfCancellationRequested(); Check();
                    if (Volatile.Read(ref _snapshot) is { } next && next.Sequence > after) return ReadValue(r, next);
                    await Task.Delay(Math.Min(5, Left()), ct).ConfigureAwait(false);
                }
            }
            return await _worker.InvokeAsync(() =>
            {
                Check(); CheckExitOperation(op, r); var current = r with { TimeoutMs = Left() }; _api.SetTimeout(_handle, (uint)current.TimeoutMs);
                return ExecuteNative(op, current, ct);
            }, Left(), ct).ConfigureAwait(false);
        }
        finally { _operations.Release(); }
    }

    private object? ExecuteNative(string op, ZlgRequest r, CancellationToken ct)
    {
        ushort Slave() => checked((ushort)r.Get<int>("SlaveIndex"));
        switch (op)
        {
            case "StartBus":
                RequireStoppedExchange();
                if (_api.IsBusRunning(_handle)) throw new InvalidOperationException("总线已经运行，请先停止");
                var path = Path.GetFullPath(r.Get<string>("EniPath"));
                EniDetails eni;
                EniProcessImageLayout layout;
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var configuration = EniValidation.Read(file, r.Get<int>("ExpectedCycleUs"), r.Get<bool>("RequireDc"));
                    file.Position = 0;
                    layout = EniProcessImageLayout.Read(file);
                    _configured = false; _eni = null; _layout = null; _ranges.Clear();
                    _api.StartBus(_handle, path); _requireDc = r.Get<bool>("RequireDc");
                    eni = _api.GetEniInfo(_handle);
                    if (eni.MainCycle != configuration.CycleUs) { _worker.Isolate("SDK 返回周期与 ENI 不一致"); throw new InvalidOperationException("SDK 返回主周期与预期不一致，通道已隔离"); }
                }
                if (eni.InputBytes > 16 * 1024 * 1024 || eni.OutputBytes > 16 * 1024 * 1024) throw new InvalidOperationException("过程映像超过插件容量上限");
                var discoveredRanges = new Dictionary<ushort, (PiRange Input, PiRange Output)>();
                for (ushort i = 0, count = _api.GetSlaveCount(_handle); i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var input = _api.GetSlavePiRange(_handle, PiArea.LocalInput, i);
                    var output = _api.GetSlavePiRange(_handle, PiArea.LocalOutput, i);
                    ValidateEniRange(input, eni.InputBytes, i, "输入");
                    ValidateEniRange(output, eni.OutputBytes, i, "输出");
                    discoveredRanges.Add(i, (input, output));
                }
                foreach (var pair in discoveredRanges) _ranges.Add(pair.Key, pair.Value);
                _eni = eni;
                _layout = layout;
                _configured = false; _output = []; Invalidate(); return _eni;
            case "StopBus": RequireStoppedExchange(); _api.StopBus(_handle); Invalidate(); _configured = false; return true;
            case "ResetChannel": RequireStoppedExchange(); if (_api.IsBusRunning(_handle)) throw new InvalidOperationException("复位前请先停止总线"); _api.ResetChannel(_handle); _configured = false; _ranges.Clear(); _eni = null; _layout = null; Invalidate(); return true;
            case "ReadMasterStatus": return new { Board = _board, BusStatus = _api.GetBusStatus(_handle), Running = _api.IsBusRunning(_handle), State = _api.GetMasterState(_handle), RequireDc = _requireDc, ActiveBoardOutputs = BoardOutputStatus(), BoardLatchOwned = _boardLatchOwned, BoardLatchStopUnconfirmed = _boardLatchNeedsStop || _boardLatchNeedsDisable };
            case "ReadEniInfo": return _api.GetEniInfo(_handle);
            case "CheckEni": _api.CheckRunningEni(_handle, Path.GetFullPath(r.Get<string>("EniPath"))); return true;
            case "ConfigureReinit": RequireStoppedExchange(); if (_api.IsBusRunning(_handle)) throw new InvalidOperationException("重初始化模式必须在启动总线前配置"); _api.SetPiReinitMode(_handle, (Native.PiReinitMode)(int)r.Get<Models.PiReinitMode>("ReinitMode")); return true;
            case "ConfigureExchange":
                RequireStoppedExchange(); if (_eni == null) throw new InvalidOperationException("请先启动总线并加载 ENI");
                var initial = r.Get<byte[]>("InitialOutput"); if (initial.Length != _eni.OutputBytes) throw new ArgumentException("必须显式提供与完整输出映像等长的初值；不能推断安全零值");
                var requestedMode = r.Get<ExchangeMode>("ExchangeMode");
                if (requestedMode == ExchangeMode.Queue && (_queueCapacity == 0 || r.Get<int>("QueuePrefill") > _queueCapacity)) throw new ArgumentException("队列预填超过 SDK 报告的容量");
                _configured = false;
                _mode = requestedMode;
                _api.SetPiAccessMode(_handle, _mode switch { ExchangeMode.Direct => PiAccessMode.Direct, ExchangeMode.Manual => PiAccessMode.Manual, _ => PiAccessMode.Cycle });
                _output = initial.ToArray();
                if (_output.Length != 0)
                {
                    _api.WritePi(_handle, OutputArea, 0, _output);
                    if (_mode == ExchangeMode.Queue) for (int i = 0; i < r.Get<int>("QueuePrefill"); i++) _api.PushOutput(_handle, false, 1);
                }
                _configured = true; return new { Mode = _mode, OutputBytes = _output.Length };
            case "EnablePi": if (!_configured) throw new InvalidOperationException("请先配置交换模式及输出初值"); RequireOp(); _api.EnablePi(_handle, true); return _api.IsPiEnabled(_handle) ? true : throw new InvalidOperationException("过程映像交换使能未确认");
            case "DisablePi": RequireNoAxes(); if (_acquiring || _outputRunning) throw new InvalidOperationException("请先停止采集和周期输出"); _api.EnablePi(_handle, false); Invalidate(); return !_api.IsPiEnabled(_handle) ? true : throw new InvalidOperationException("过程映像交换关闭未确认");
            case "ReadPiRange":
                var ranges = Range(Slave());
                var inputFields = _layout?.Describe(ranges.Input, true) ?? [];
                var outputFields = _layout?.Describe(ranges.Output, false) ?? [];
                return new { SlaveIndex = Slave(), ranges.Input, ranges.Output,
                    HasMetadata = inputFields.Count != 0 || outputFields.Count != 0,
                    InputMetadataAvailable = inputFields.Count != 0, OutputMetadataAvailable = outputFields.Count != 0,
                    LayoutHasMetadata = _layout?.HasMetadata ?? false, MetadataCoverage = "仅表示此范围内存在字段，不保证完整覆盖或对象身份已核验",
                    InputFields = inputFields, OutputFields = outputFields, ObjectIdentityVerified = false };
            case "EnumerateSlaves": return ZlgSdkOperations.DescribeSlaves(_api, _handle, Range, ct);
            case "ReadSlaveInfo": return ZlgSdkOperations.DescribeSlaveInfo(_api, _handle, Slave(), ct);
            case "ReadSlaveStatus": return ZlgSdkOperations.DescribeSlaveStatus(_api, _handle, Slave(), ct);
            case "StartAcquisition":
                if (!_configured || !_api.IsPiEnabled(_handle)) throw new InvalidOperationException("请先配置并使能过程映像交换");
                if (_eni == null || _eni.InputBytes == 0) throw new InvalidOperationException("当前 ENI 没有输入过程映像，不能启动输入采集；输出功能可继续使用。");
                if (_acquiring) throw new InvalidOperationException("采集已经运行");
                int nativeWait = r.Get<int>("NativeWaitMs"), historyCapacity = r.Get<int>("HistoryCapacity");
                int highWater = r.GetOrDefault("RxHighWatermarkPercent", 80), backlogWarning = r.GetOrDefault("RxBacklogWarningMs", 10), alarmRepeat = r.GetOrDefault("RxAlarmRepeatMs", 1000);
                if (nativeWait is < 1 or > 1000 || historyCapacity is < 1 or > 1000000 || highWater is < 1 or > 100 || backlogWarning is < 1 or > 60000 || alarmRepeat is < 100 or > 3600000)
                    throw new ArgumentException("采集等待、历史容量或 RX 告警参数超出允许范围");
                bool latestOnly = r.Get<bool>("LatestOnly");
                _worker.SetAcquisitionPriority(r.GetOrDefault("AcquisitionThreadPriority", AcquisitionPriority.Normal));
                Check();
                var monitor = new RxQueueMonitor();
                monitor.Reset(_mode == ExchangeMode.Queue, _queueCapacity, highWater, backlogWarning, alarmRepeat, latestOnly, Stopwatch.GetTimestamp());
                Volatile.Read(ref _rx).Stop();
                Volatile.Write(ref _rx, monitor);
                _nativeWaitMs = nativeWait; _historyCapacity = historyCapacity; _latestOnly = latestOnly;
                Invalidate(); lock (_state) _error = null;
                _acquiring = true; return true;
            case "StopAcquisition":
                RequireNoAxes(); if (_pools.Count != 0) throw new InvalidOperationException("请先关闭依赖采集的功能池");
                _acquiring = false; Volatile.Read(ref _rx).Stop(); Invalidate();
                _worker.RestoreAcquisitionPriority(); return true;
            case "ReadPdo": return ReadValue(r, Snapshot(r.Get<int>("MaxAgeMs")));
            case "ReadPdoBits": case "ReadDigitalInput":
                var snap = Snapshot(r.Get<int>("MaxAgeMs")); int bit = r.Get<int>("BitOffset"), bits = r.GetOrDefault("BitLength", 1);
                int offset = Absolute(Range(Slave()).Input, r.Get<int>("Offset"), (bit + bits + 7) / 8);
                ulong value = ZlgDataCodec.Bits(snap.Data.AsSpan(offset, (bit + bits + 7) / 8).ToArray(), bit, bits);
                return op == "ReadDigitalInput" ? (object)(value != 0) : value;
            case "ReadAnalogInput":
                var type = r.Get<PdoDataType>("DataType"); var image = Snapshot(r.Get<int>("MaxAgeMs"));
                var index = Absolute(Range(Slave()).Input, r.Get<int>("Offset"), ZlgDataCodec.Size(type));
                _layout?.ValidateScalar(index, ZlgDataCodec.Size(type), type, true);
                return Convert.ToDouble(ZlgDataCodec.Decode(image.Data.AsSpan(index, ZlgDataCodec.Size(type)).ToArray(), type)) * r.Get<double>("Scale") + r.Get<double>("Bias");
            case "WritePdo": case "WritePdoMasked":
                RejectClaim(Slave());
                var rawOutput = r.Get<byte[]>("Data");
                var rawType = r.GetOrDefault("DataType", PdoDataType.Bytes);
                if (rawType != PdoDataType.Bytes && rawOutput.Length != ZlgDataCodec.Size(rawType)) throw new ArgumentException("PDO 写入数据长度与声明的数据类型不一致");
                _layout?.ValidateScalar(Absolute(Range(Slave()).Output, r.Get<int>("Offset"), rawOutput.Length), rawOutput.Length, rawType, false);
                WriteOutput(Slave(), r.Get<int>("Offset"), rawOutput, op == "WritePdoMasked" ? r.Get<byte[]>("Mask") : null); return true;
            case "WriteDigitalOutput":
                RejectClaim(Slave()); int b = r.Get<int>("BitOffset"); if (b is < 0 or > 7) throw new ArgumentException("数字输出位必须在 0～7");
                WriteOutput(Slave(), r.Get<int>("Offset"), [(byte)(r.Get<bool>("Value") ? 1 << b : 0)], [(byte)(1 << b)]); return true;
            case "WriteAnalogOutput":
                RejectClaim(Slave()); double scale = r.Get<double>("Scale"); if (!double.IsFinite(scale) || scale == 0) throw new ArgumentException("缩放系数必须为非零有限数");
                var outputType = r.Get<PdoDataType>("DataType");
                var encoded = ZlgDataCodec.Encode((r.Get<double>("Value") - r.Get<double>("Bias")) / scale, outputType);
                _layout?.ValidateScalar(Absolute(Range(Slave()).Output, r.Get<int>("Offset"), encoded.Length), encoded.Length, outputType, false);
                WriteOutput(Slave(), r.Get<int>("Offset"), encoded, null); return true;
            case "StartCyclicOutput":
                RequireOp(); if (!_configured || !_api.IsPiEnabled(_handle)) throw new InvalidOperationException("请先配置并使能交换");
                if (_output.Length == 0) throw new InvalidOperationException("当前 ENI 没有输出过程映像，不能启动周期输出。");
                if (_mode == ExchangeMode.Queue) lock (_state) foreach (var axis in _claims.Values) axis.ValidateQueueOutput();
                _outputIntervalMs = r.Get<int>("IntervalMs"); _commandLeaseMs = r.Get<int>("CommandLeaseMs");
                if (_commandLeaseMs <= _outputIntervalMs) throw new ArgumentException("输出命令有效期必须大于输出间隔");
                _lastOutputCommand = Stopwatch.GetTimestamp(); _nextOutput = 0; _outputRunning = true; return true;
            case "StopCyclicOutput": RequireNoAxes(); _outputRunning = false; return new { Stopped = true, QueuedFramesWithdrawn = false };
            case "ReadExchangeStatus": return new { Mode = _mode, PiEnabled = _api.IsPiEnabled(_handle), OutputRunning = _outputRunning, InputQueue = _api.GetInputQueueSize(_handle), OutputQueue = _api.GetOutputQueueSize(_handle), Isolated = _worker.IsolatedReason };
            case "ReadHistory": lock (_state) return new { Dropped = _historyDropped, DroppedMeaning = "LocalHistoryEvictions", HardwareDroppedFrames = (long?)null, OldestSequence = _history.TryPeek(out var first) ? first.Sequence : 0, Samples = _history.Where(s => s.Sequence > r.Get<long>("AfterSequence")).Take(r.Get<int>("MaxSamples")).Select(s => s with { Data = s.Data.ToArray() }).ToArray() };
            default:
                if (ZlgSdkOperations.Handles(op))
                {
                    if (_outputRunning && _mode == ExchangeMode.Queue && op is "ReadSdo" or "WriteSdo" or "FoeRead" or "FoeWrite" or "ReadSlaveMemory" or "ReadSlaveEeprom" or "ReadCapture")
                        throw new InvalidOperationException("队列周期输出期间禁止长时间邮箱/维护/抓包调用占用唯一 SDK 线程；请停止周期输出后执行参数读写");
                    if (op.StartsWith("Write", StringComparison.Ordinal) || op.StartsWith("Configure", StringComparison.Ordinal) || op is "FoeWrite" or "ResetBoard" or "BoardUpdate" or "BoardFileWrite" or "DisableForceOutput") RequireNoAxes();
                    if (op is "FoeWrite" or "WriteSlaveMemory" or "WriteSlaveEeprom" or "ResetBoard" or "BoardUpdate" or "BoardFileWrite") RequireStoppedExchange();
                    using var maintenance = op is "ResetBoard" or "BoardUpdate" or "BoardFileWrite" or "BoardFileRead" ? _boardMaintenance() : null;
                    return ExecuteSdkOperation(op, r, ct);
                }
                throw new NotSupportedException($"未注册的 ZLG 操作：{op}");
        }
    }

    private PiArea OutputArea => _mode == ExchangeMode.Queue ? PiArea.LocalOutput : PiArea.FastOutput;
    private static void ValidateEniRange(PiRange range, uint imageLength, ushort slave, string direction)
    {
        if (range.Length != 0 && (ulong)range.Offset + range.Length > imageLength)
            throw new InvalidDataException($"从站 {slave} 的{direction}映像范围 {range.Offset}+{range.Length} 超出 ENI 完整映像 {imageLength} 字节。");
    }
    private object? ExecuteSdkOperation(string op, ZlgRequest request, CancellationToken ct)
    {
        bool startsPwm = op == "StartBoardPwm";
        bool startsPulse = op == "OutputBoardPulse" || op == "ConfigureBoardPulse" && request.Get<bool>("Continuous");
        bool stopsPwm = op == "StopBoardPwm", stopsPulse = op == "StopBoardPulse";
        bool boardOutputOperation = startsPwm || startsPulse || stopsPwm || stopsPulse || op == "ConfigureBoardPulse";
        bool latchOperation = op is "ConfigureBoardLatch" or "StartBoardLatch" or "ReadBoardLatch" or "StopBoardLatch" or "ResetBoardLatchCounter";
        IDisposable? temporaryLease = null;
        (bool Pwm, uint Port) key = default;
        if (boardOutputOperation || latchOperation)
        {
            if (!_board.Name.Equals("PCIe-1E16", StringComparison.OrdinalIgnoreCase) && !_board.Name.Equals("PCIe-1E16-SM", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"板卡 {_board.Name} 未核实板载 PWM/脉冲能力，当前仅开放 PCIe-1E16。");
            if (boardOutputOperation)
            {
                key = (startsPwm || stopsPwm, request.Get<uint>("Port"));
                if (key.Port > 3) throw new ArgumentOutOfRangeException("Port", "板载输出通道范围为 0～3。");
            }
            if ((op is "ConfigureBoardLatch" or "StartBoardLatch" or "ResetBoardLatchCounter") && (_boardLatchNeedsStop || _boardLatchNeedsDisable))
                throw new InvalidOperationException("锁存仍在运行或停止结果未确认，请先执行 StopBoardLatch，再修改配置、重新启动或复位计数器。");
            if (op == "StartBoardLatch" && _sdkState.LatchContinuous != request.Get<bool>("Continuous"))
                throw new InvalidOperationException("本连接尚未成功应用相同模式的锁存配置，不能启动锁存。");
            if (startsPwm || startsPulse || (op is "ConfigureBoardLatch" or "StartBoardLatch"))
            {
                // 先取得板卡独占，再登记可能发生的输出。原生异常不能证明未输出。
                if (_boardOutputLease == null) _boardOutputLease = _boardMaintenance();
                if (latchOperation)
                {
                    _boardLatchOwned = true;
                    if (op == "StartBoardLatch") { _boardLatchNeedsStop = true; _boardLatchNeedsDisable = true; }
                }
                else lock (_state) _activeBoardOutputs.Add(key);
            }
            else if (_boardOutputLease == null) temporaryLease = _boardMaintenance();
        }
        try
        {
            var result = ZlgSdkOperations.Execute(op, request, _api, _handle, _board, ct, _sdkState);
            if (stopsPwm || stopsPulse) CompleteBoardOutputStop(key);
            if (op == "StopBoardLatch") { _boardLatchNeedsStop = false; _boardLatchNeedsDisable = false; }
            return result;
        }
        finally { temporaryLease?.Dispose(); }
    }
    private void CompleteBoardOutputStop((bool Pwm, uint Port) key)
    {
        lock (_state) _activeBoardOutputs.Remove(key);
        ReleaseBoardLeaseIfIdle();
    }
    private void ReleaseBoardLeaseIfIdle()
    {
        IDisposable? release = null;
        lock (_state)
        {
            if (_activeBoardOutputs.Count == 0 && !_boardLatchOwned) { release = _boardOutputLease; _boardOutputLease = null; }
        }
        release?.Dispose();
    }
    private object BoardOutputStatus()
    {
        lock (_state) return _activeBoardOutputs.Select(output => new { Kind = output.Pwm ? "PWM" : "Pulse", output.Port }).ToArray();
    }
    private void Invalidate() { Volatile.Write(ref _snapshot, null); lock (_state) { _history.Clear(); _historyDropped = 0; } }
    private InputSnapshot Snapshot(int age)
    {
        var snapshot = Volatile.Read(ref _snapshot) ?? throw new InvalidOperationException("尚未收到输入映像，请先启动采集");
        if (!_acquiring || !snapshot.LocallyValid || snapshot.LocalAgeMs > age) throw new InvalidOperationException("输入映像无效、积压或已超过本地接收有效期");
        return snapshot;
    }
    private object ReadValue(ZlgRequest r, InputSnapshot snapshot)
    {
        Check();
        if (!_acquiring || !snapshot.LocallyValid || snapshot.LocalAgeMs > r.Get<int>("MaxAgeMs")) throw new InvalidOperationException("读取项输入无效、积压或过期");
        int length = r.Get<int>("Length"), offset = Absolute(Range(checked((ushort)r.Get<int>("SlaveIndex"))).Input, r.Get<int>("Offset"), length);
        var type = r.Get<PdoDataType>("DataType");
        _layout?.ValidateScalar(offset, length, type, true);
        var value = ZlgDataCodec.Decode(snapshot.Data.AsSpan(offset, length).ToArray(), type);
        return r.GetOrDefault("ReturnSample", false)
            ? new PdoReadSample(value, snapshot.Sequence, checked((ushort)r.Get<int>("SlaveIndex")), r.Get<int>("Offset"), length, type, snapshot.ReceivedTimestamp, snapshot.LocalAgeMs)
            : value;
    }
    private void WriteOutput(ushort slave, int offset, byte[] data, byte[]? mask)
    {
        RequireLeaseHealthy();
        RequireOp(); if (!_configured || !_api.IsPiEnabled(_handle)) throw new InvalidOperationException("过程映像交换未使能");
        int at = Absolute(Range(slave).Output, offset, data.Length);
        if (mask != null && mask.Length != data.Length) throw new ArgumentException("掩码和输出数据长度必须一致");
        var updated = _output.ToArray();
        for (int i = 0; i < data.Length; i++) updated[at + i] = mask == null ? data[i] : (byte)((updated[at + i] & ~mask[i]) | (data[i] & mask[i]));
        _output = updated; _lastOutputCommand = Stopwatch.GetTimestamp(); _pendingOutput = true;
    }
    private void SendOutput()
    {
        if (_output.Length == 0) throw new InvalidOperationException("没有可提交的输出映像");
        _api.WritePi(_handle, OutputArea, 0, _output);
        if (_mode == ExchangeMode.Queue) _api.PushOutput(_handle, false, 1);
        else if (_mode == ExchangeMode.Manual) _api.UpdateAsyncPi(_handle, PiArea.FastOutput);
    }
    private void Tick()
    {
        if (_handle == IntPtr.Zero || _closing) return;
        RequireLeaseHealthy();
        if (_outputRunning || _pendingOutput || _motionFrames.Count != 0)
        {
            if (!_api.IsBusRunning(_handle) || !_api.IsPiEnabled(_handle)) throw new InvalidOperationException("输出期间总线或过程映像交换失效");
            RequireOp();
        }
        // Preserve control-word edges. Each motion command is submitted before its receipt completes.
        while (_motionFrames.TryPeek(out _))
        {
            if (_mode == ExchangeMode.Queue && _api.GetOutputQueueSize(_handle) >= _queueCapacity) break;
            var frame = _motionFrames.Dequeue();
            try
            {
                if (_exitRequested && !frame.StopLease) { frame.Receipt.TrySetException(new UnsubmittedMotionException("退出闭锁已撤回尚未提交的运动目标")); continue; }
                frame.Token.ThrowIfCancellationRequested();
                var next = _output.ToArray();
                foreach (var write in frame.Writes) write.Data.CopyTo(next, write.ByteOffset);
                _output = next; SendOutput(); _lastOutputCommand = Stopwatch.GetTimestamp();
                lock (_state)
                {
                    if (frame.StopLease) _leases.Remove(frame.Axis);
                    else if (frame.RenewLease) _leases[frame.Axis] = _lastOutputCommand;
                }
                frame.Receipt.TrySetResult();
            }
            catch (Exception ex) { frame.Receipt.TrySetException(ex); throw; }
        }
        if (_outputRunning)
        {
            if (_mode == ExchangeMode.Queue && _eni != null)
            {
                // Firmware owns the bus clock. Replenish a bounded look-ahead queue in bursts.
                long aheadUs = Math.Min(10000L, _commandLeaseMs * 250L);
                uint target = Math.Min(_queueCapacity, (uint)Math.Clamp((aheadUs + _eni.MainCycle - 1) / Math.Max(1, _eni.MainCycle), 1, 64));
                uint depth = _api.GetOutputQueueSize(_handle);
                for (uint i = depth; i < target; i++) { SendOutput(); if (_closing) break; }
                _pendingOutput = false;
            }
            else if (Stopwatch.GetTimestamp() >= _nextOutput || _pendingOutput)
            { SendOutput(); _pendingOutput = false; _nextOutput = Stopwatch.GetTimestamp() + (long)(_outputIntervalMs * Stopwatch.Frequency / 1000.0); }
        }
        else if (_pendingOutput && (_mode != ExchangeMode.Queue || _api.GetOutputQueueSize(_handle) < _queueCapacity)) { SendOutput(); _pendingOutput = false; }
        if (!_acquiring || _eni == null) return;
        if (!_api.IsBusRunning(_handle) || !_api.IsPiEnabled(_handle) || _api.GetMasterState(_handle) != 8)
            throw new InvalidOperationException("采集期间总线、OP 或过程映像交换已失效");
        if (_requireDc && (_api.ReadDiagnostics(_handle, []).State & 0xA01) != 0xA01)
            throw new InvalidOperationException("采集期间 DC 同步状态失效");
        var monitor = Volatile.Read(ref _rx);
        if (_mode == ExchangeMode.Queue) monitor.BeginPoll(Stopwatch.GetTimestamp());
        try
        {
            int budget = _mode == ExchangeMode.Queue ? 32 : 1;
            while (budget-- > 0)
            {
                uint backlog = 0;
                if (_mode == ExchangeMode.Queue)
                {
                    uint before = _api.GetInputQueueSize(_handle);
                    monitor.ObserveDepth(before, Stopwatch.GetTimestamp());
                    if (before == 0 || !_api.PopInput(_handle, _latestOnly, (uint)_nativeWaitMs)) return;
                    backlog = _api.GetInputQueueSize(_handle);
                    monitor.ObserveDepth(backlog, Stopwatch.GetTimestamp());
                }
                else if (_mode == ExchangeMode.Manual) _api.UpdateAsyncPi(_handle, PiArea.ShadowInput);
                var data = _api.ReadPi(_handle, _mode == ExchangeMode.Queue ? PiArea.LocalInput : PiArea.ShadowInput, 0, checked((int)_eni.InputBytes));
                var snapshot = new InputSnapshot(++_sequence, Stopwatch.GetTimestamp(), data.ToArray(), backlog, backlog == 0, _mode == ExchangeMode.Queue);
                Volatile.Write(ref _snapshot, snapshot);
                lock (_state) { _history.Enqueue(snapshot); while (_history.Count > _historyCapacity) { _history.Dequeue(); _historyDropped++; } }
                if (snapshot.LocallyValid) { _background.Publish(snapshot); _periodic.Publish(snapshot); }
            }
        }
        finally { if (_mode == ExchangeMode.Queue) _background.WakeMaintenance(); }
    }

    private object AcquisitionStatus()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        string? error; lock (_state) error = _error;
        return new { Running = _acquiring, snapshot?.Sequence, AgeMs = snapshot?.LocalAgeMs, snapshot?.QueueBacklog, snapshot?.LocallyValid,
            Error = error, Isolated = _worker.IsolatedReason, WorkerStarts = _worker.WorkerStarts, Priority = _worker.PriorityStatus,
            RxQueue = Volatile.Read(ref _rx).Read(Stopwatch.GetTimestamp()), HostReadTimestamp.TimestampSource, HostReadTimestamp.TimestampFrequencyHz,
            HostReadTimestamp.HardwareTimestampAvailable, HostReadTimestamp.HardwareTimestamp, HardwareSampleAgeMs = (double?)null };
    }

    private void DeliverRxNotice()
    {
        if (_closing || _warning == null) return;
        var monitor = Volatile.Read(ref _rx);
        var notice = monitor.TryTakeNotice(Stopwatch.GetTimestamp());
        if (notice == null || _closing || !ReferenceEquals(monitor, Volatile.Read(ref _rx))) return;
        try { _warning(notice); }
        // A previous acquisition's entered callback may finish after restart; its failure stays with that monitor.
        catch (Exception ex) { monitor.RecordNotificationFailure(ex); }
    }
    private void RequireLeaseHealthy()
    {
        if (!_outputRunning) return;
        bool expired = Stopwatch.GetElapsedTime(_lastOutputCommand).TotalMilliseconds > _commandLeaseMs;
        lock (_state) expired |= _leases.Any(kv => Stopwatch.GetElapsedTime(kv.Value).TotalMilliseconds > _commandLeaseMs);
        if (expired)
        {
            _outputRunning = false; _worker.Isolate("周期输出命令或独立轴目标已过期，停止续发且禁止新命令掩盖断供");
            throw new TimeoutException("周期输出命令或独立轴目标已过期；软件停止续发不保证驱动停稳");
        }
    }

    private async Task<object> PoolAsync(string op, ZlgRequest r, IExecutionContext context, CancellationToken ct)
    {
        bool periodic = op.StartsWith("Periodic", StringComparison.Ordinal);
        var pool = periodic ? _periodic : _background;
        string name = r.Get<string>("PoolName"), key = (periodic ? "P:" : "B:") + name;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("功能池名称不能为空");
        if (op.EndsWith("Create", StringComparison.Ordinal))
        {
            if (!_acquiring) throw new InvalidOperationException("请先启动唯一输入采集器，再建立读取功能池");
            if (_pools.Count >= 128 || !_pools.TryAdd(key, (periodic, r.Get<int>("MaxItems"), new(StringComparer.Ordinal)))) throw new InvalidOperationException("功能池已存在或达到数量上限"); return true;
        }
        if (!_pools.TryGetValue(key, out var definition)) throw new InvalidOperationException("功能池不存在，请先创建");
        if (op.EndsWith("Status", StringComparison.Ordinal)) return new { WorkerStarts = pool.WorkerStarts, InputAgeMs = Volatile.Read(ref _snapshot)?.LocalAgeMs, AcquisitionRunning = _acquiring, Isolated = _worker.IsolatedReason,
            Priority = _worker.PriorityStatus, RxQueue = Volatile.Read(ref _rx).Read(Stopwatch.GetTimestamp()), HostReadTimestamp.TimestampSource, HostReadTimestamp.TimestampFrequencyHz,
            HostReadTimestamp.HardwareTimestampAvailable, HostReadTimestamp.HardwareTimestamp, HardwareSampleAgeMs = (double?)null,
            Items = pool.Status().Where(i => definition.Items.Contains(i.Name)).ToArray() };
        if (op.EndsWith("Close", StringComparison.Ordinal))
        { foreach (var item in definition.Items.ToArray()) { await pool.RemoveAsync(item, ct).ConfigureAwait(false); definition.Items.Remove(item); } _pools.Remove(key); return true; }
        string itemName = r.Get<string>("ItemName"); if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentException("读取项名称不能为空");
        string itemKey = key.Length + ":" + key + itemName;
        if (op.EndsWith("StopItem", StringComparison.Ordinal)) { await pool.RemoveAsync(itemKey, ct).ConfigureAwait(false); definition.Items.Remove(itemKey); return true; }
        string variable = r.Get<string>("DataVariable");
        if (string.IsNullOrWhiteSpace(variable) || !context.HasVariable(variable)) throw new ArgumentException("读取数据变量必须已声明");
        int absoluteOffset = Absolute(Range(checked((ushort)r.Get<int>("SlaveIndex"))).Input, r.Get<int>("Offset"), r.Get<int>("Length"));
        var registry = context.Resources;
        var epoch = PluginCommon.ExecutionEpoch.Capture(registry, "ZlgEtherCat");
        var type = r.Get<PdoDataType>("DataType");
        if (type != PdoDataType.Bytes && r.Get<int>("Length") != ZlgDataCodec.Size(type)) throw new ArgumentException("读取长度与数据类型不一致");
        _layout?.ValidateScalar(absoluteOffset, r.Get<int>("Length"), type, true);
        if (op.EndsWith("UpdateItem", StringComparison.Ordinal)) { await pool.RemoveAsync(itemKey, ct).ConfigureAwait(false); definition.Items.Remove(itemKey); }
        if (definition.Items.Count >= definition.MaxItems) throw new InvalidOperationException("功能池已达到配置容量");
        ct.ThrowIfCancellationRequested();
        var disableExactItem = pool.Add(itemKey, periodic ? r.Get<int>("IntervalMs") : 1, snapshot =>
        {
            if (_closing || !ReferenceEquals(context.Resources, registry)) throw new InvalidOperationException("功能池所属执行已结束，停止发布");
            epoch.ThrowIfExpired();
            var value = ReadValue(r, snapshot);
            epoch.ThrowIfExpired();
            context.SetVariable(variable, value);
        });
        PluginCommon.ExecutionScopedExecutor.Own(disableExactItem);
        definition.Items.Add(itemKey); return true;
    }

    public void ClaimAxis(MotionAxisConfiguration configuration)
    {
        configuration.Validate();
        Check(); var range = Range(configuration.SlaveIndex);
        _layout?.ValidateMotion(configuration);
        if (_outputRunning && _mode == ExchangeMode.Queue) configuration.ValidateQueueOutput();
        if (configuration.InputByteLength > 0 && (configuration.InputByteOffset < range.Input.Offset || (long)configuration.InputByteOffset + configuration.InputByteLength > (long)range.Input.Offset + range.Input.Length)) throw new ArgumentException("轴输入映像不属于指定从站");
        if (configuration.OutputByteLength > 0 && (configuration.OutputByteOffset < range.Output.Offset || (long)configuration.OutputByteOffset + configuration.OutputByteLength > (long)range.Output.Offset + range.Output.Length)) throw new ArgumentException("轴输出映像不属于指定从站");
        lock (_state)
        {
            if (_claims.Values.Any(c => c.SlaveIndex == configuration.SlaveIndex) || !_claims.TryAdd(configuration.AxisName, configuration)) throw new InvalidOperationException("轴名称或从站已被绑定");
        }
    }
    public void ReleaseAxis(string axisName) { lock (_state) { _claims.Remove(axisName); _leases.Remove(axisName); } }
    public Task<byte[]> ReadObjectAsync(ushort slave, ushort index, byte subIndex, int expectedLength, int timeoutMs, CancellationToken ct) =>
        _worker.InvokeAsync(() => { Check(); RequireMailboxWindow(); _api.SetTimeout(_handle, (uint)timeoutMs); return _api.ReadSdo(_handle, slave, index, subIndex, expectedLength, (uint)timeoutMs); }, timeoutMs, ct);
    public Task WriteObjectAsync(ushort slave, ushort index, byte subIndex, byte[] data, int timeoutMs, CancellationToken ct)
    {
        bool cleanup = _motionOperation.Value is "StopMotion" or "DisableAxis";
        return _worker.InvokeAsync(() =>
        {
            Check(); if (_exitRequested && !cleanup) throw new InvalidOperationException("退出闭锁拒绝新运动写入");
            RequireMailboxWindow(); RequireOp(); _api.SetTimeout(_handle, (uint)timeoutMs); _api.WriteSdo(_handle, slave, index, subIndex, data, (uint)timeoutMs);
        }, timeoutMs, ct);
    }
    private void RequireMailboxWindow()
    {
        if (_outputRunning && _mode == ExchangeMode.Queue) throw new InvalidOperationException("队列周期输出期间运动反馈/控制必须具备 PDO 映射；SDO 会阻塞队列补充");
    }
    public Task<MotionSnapshot> ReadSnapshotAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Check();
        if (_mode != ExchangeMode.Queue) throw new InvalidOperationException("运动 PDO 反馈必须使用输入队列采集；异步映像不提供新帧证据");
        var s = Snapshot(int.MaxValue);
        return Task.FromResult(new MotionSnapshot(s.Data, s.Sequence, s.ReceivedTimestamp, s.LocallyValid));
    }
    public async Task SubmitPdoAsync(string axisName, IReadOnlyList<MotionPdoWrite> writes, int timeoutMs, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        string? operation = _motionOperation.Value;
        var receipt = await _worker.InvokeAsync(() =>
        {
            Check(); RequireOp(); if (!_configured || !_api.IsPiEnabled(_handle)) throw new InvalidOperationException("轴 PDO 交换未使能");
            if (_mode != ExchangeMode.Queue) throw new InvalidOperationException("运动 PDO 输出必须使用 Queue 模式，异步缓存不能保证控制字边沿逐帧提交");
            MotionAxisConfiguration axis; lock (_state) axis = _claims.TryGetValue(axisName, out var found) ? found : throw new InvalidOperationException("轴归属已失效");
            foreach (var write in writes)
            {
                if (write.ByteOffset < axis.OutputByteOffset || (long)write.ByteOffset + write.Data.Length > (long)axis.OutputByteOffset + axis.OutputByteLength) throw new ArgumentException("轴输出越界");
            }
            if (_motionFrames.Count >= 128) throw new InvalidOperationException("运动输出提交队列已满");
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _motionFrames.Enqueue(new(axisName, writes.Select(w => new MotionPdoWrite(w.ByteOffset, w.Data.ToArray())).ToArray(),
                operation is "WriteCspTarget" or "WriteCsvTarget" or "WriteCstTarget", operation is "StopMotion" or "DisableAxis", ct, completion));
            return completion.Task;
        }, timeoutMs, ct).ConfigureAwait(false);
        try
        {
            var left = timeoutMs - (int)timer.ElapsedMilliseconds;
            if (left <= 0) throw new TimeoutException("运动输出提交超过步骤期限");
            await receipt.WaitAsync(TimeSpan.FromMilliseconds(left), ct).ConfigureAwait(false);
        }
        catch (UnsubmittedMotionException) { throw; }
        catch
        {
            _worker.Isolate("运动输出提交结果未确认，禁止继续提交；需退出通道");
            _ = receipt.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    internal Task CloseAsync()
    {
        lock (_state)
        {
            if (_closeTask != null) return _closeTask;
            _closing = true; _acquiring = false; Volatile.Read(ref _rx).Stop(); _background.Dispose(); _periodic.Dispose();
            return _closeTask = FinishCloseAsync();
        }
    }
    private async Task FinishCloseAsync()
    {
        await Task.Yield(); // Resource disposal must never run native close or user callbacks inline.
        bool nativeCleanupSucceeded = false;
        var poolsDrained = Task.WhenAll(_background.Completion, _periodic.Completion);
        var nativeClose = _worker.CloseAsync(() =>
        {
            while (_motionFrames.TryDequeue(out var frame)) frame.Receipt.TrySetException(new InvalidOperationException("通道退出，运动输出未提交"));
            if (_handle != IntPtr.Zero)
            {
                var errors = new List<Exception>();
                (bool Pwm, uint Port)[] activeOutputs;
                lock (_state) activeOutputs = _activeBoardOutputs.ToArray();
                foreach (var output in activeOutputs)
                {
                    try
                    {
                        var boardHandle = _api.GetBoardHandle(_handle);
                        if (output.Pwm) _api.StopPwm(boardHandle, output.Port); else _api.StopPulse(boardHandle, output.Port);
                        CompleteBoardOutputStop(output);
                    }
                    catch (Exception ex) { errors.Add(new InvalidOperationException($"退出时未确认板载 {(output.Pwm ? "PWM" : "脉冲")} 端口 {output.Port} 已停止，保留板卡归属。", ex)); }
                }
                if (_boardLatchOwned)
                {
                    if (_boardLatchNeedsStop)
                    {
                        try { _api.StopLatch(_api.GetBoardHandle(_handle)); _boardLatchNeedsStop = false; }
                        catch (Exception ex) { errors.Add(new InvalidOperationException("退出时未确认板载锁存已停止，保留板卡归属。", ex)); }
                    }
                    if (_boardLatchNeedsDisable)
                    {
                        try { _api.EnableLatch(_api.GetBoardHandle(_handle), false); _boardLatchNeedsDisable = false; }
                        catch (Exception ex) { errors.Add(new InvalidOperationException("退出时未确认板载锁存已禁用，保留板卡归属。", ex)); }
                    }
                    if (!_boardLatchNeedsStop && !_boardLatchNeedsDisable)
                    {
                        _boardLatchOwned = false;
                        ReleaseBoardLeaseIfIdle();
                    }
                }
                try { _api.EnablePi(_handle, false); } catch (Exception ex) { errors.Add(ex); }
                try { _api.StopBus(_handle); } catch (Exception ex) { errors.Add(ex); }
                try { _api.Close(_handle); _handle = IntPtr.Zero; } catch (Exception ex) { errors.Add(ex); }
                if (errors.Count != 0) throw new AggregateException("退出有未确认的原生操作，物理通道继续隔离", errors);
            }
            nativeCleanupSucceeded = true;
        });
        try { await Task.WhenAll(nativeClose, poolsDrained).ConfigureAwait(false); }
        finally
        {
            // Slow host callbacks cannot defer device cleanup, but keep ownership until they really exit.
            // A priority-restore failure alone does not invalidate successful native cleanup.
            if (nativeCleanupSucceeded && poolsDrained.IsCompletedSuccessfully) _releaseReservation();
        }
        bool abandoned; lock (_state) abandoned = _claims.Count != 0;
        if (abandoned) Report(new InvalidOperationException("执行结束时仍有绑定轴：通信已退出，但未确认驱动停稳；正常流程必须先停止、去使能、解绑。"));
    }
    public void Dispose()
    {
        var actual = CloseAsync();
        _ = actual.ContinueWith(t => Report(t.Exception!.GetBaseException()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    private void Report(Exception error)
    {
        try { _report(error); }
        catch (Exception logging) { lock (_state) _error = new AggregateException(error, logging).ToString(); }
    }
}
