using System.Buffers.Binary;
using System.Diagnostics;

namespace ZlgEtherCat.Motion;

/// <summary>独立 CiA402 工艺状态机。每次调用有界，不创建采集线程，不拥有父连接。</summary>
public sealed partial class ZlgMotionAxis
{
    private readonly IZlgMotionIo _io;
    private readonly SemaphoreSlim _operation = new(1, 1);
    private readonly MotionAxisConfiguration _configuration;
    private readonly object _controlSync = new();
    private CancellationTokenSource? _activeControl;
    private long _controlEpoch;
    private readonly AsyncLocal<long> _deadlineTimestamp = new();
    private bool _released;
    private bool _controlUncertain;
    private bool _hasWrittenControl;
    public string AxisName => _configuration.AxisName;
    public MotionAxisConfiguration Configuration => _configuration with { AllowedModes = (MotionMode[])_configuration.AllowedModes.Clone() };

    private ZlgMotionAxis(IZlgMotionIo io, MotionAxisConfiguration configuration)
    {
        _io = io;
        _configuration = configuration with { AllowedModes = (MotionMode[])configuration.AllowedModes.Clone() };
    }

    public static async Task<ZlgMotionAxis> BindAsync(IZlgMotionIo io, MotionAxisConfiguration configuration, int timeoutMs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "超时必须大于零。");
        ct.ThrowIfCancellationRequested();
        var axis = new ZlgMotionAxis(io, configuration);
        io.ClaimAxis(axis._configuration);
        try
        {
            await axis.RunAsync(async token =>
            {
                await axis.ReadStatusCoreAsync(token).ConfigureAwait(false);
                await axis.ReadModeCoreAsync(token).ConfigureAwait(false);
                return true;
            }, timeoutMs, ct).ConfigureAwait(false);
            return axis;
        }
        catch (Exception error)
        {
            if (!axis._operation.Wait(0))
                throw new InvalidOperationException("绑定轴失败且原访问尚未退出；保留轴占用，请完成通道清理后重试。", error);
            try { io.ReleaseAxis(configuration.AxisName); }
            finally { axis._operation.Release(); }
            throw;
        }
    }

    public Task<MotionDriveState> ReadStateAsync(int timeoutMs, CancellationToken ct) => RunAsync(async token => DecodeState(await ReadStatusCoreAsync(token).ConfigureAwait(false)), timeoutMs, ct);
    public Task<sbyte> ReadModeAsync(int timeoutMs, CancellationToken ct) => RunAsync(ReadModeCoreAsync, timeoutMs, ct);
    public Task<MotionFeedback> ReadFeedbackAsync(int timeoutMs, CancellationToken ct) => RunAsync(ReadFeedbackCoreAsync, timeoutMs, ct);
    public Task<MotionDiagnostics> ReadDiagnosticsAsync(int timeoutMs, CancellationToken ct) => RunAsync(async token =>
    {
        var status = await ReadStatusCoreAsync(token).ConfigureAwait(false);
        var mode = await ReadModeCoreAsync(token).ConfigureAwait(false);
        var error = BinaryPrimitives.ReadUInt16LittleEndian(await ReadObjectAsync(0x603F, 0, 2, token).ConfigureAwait(false));
        var register = (await ReadObjectAsync(0x1001, 0, 1, token).ConfigureAwait(false))[0];
        return new MotionDiagnostics(status, DecodeState(status), mode, error, register);
    }, timeoutMs, ct);

    public Task SetModeAsync(MotionMode mode, int timeoutMs, CancellationToken ct) => ControlAsync(async token =>
    {
        RequireModeAllowed(mode);
        if (await ReadModeCoreAsync(token).ConfigureAwait(false) == (sbyte)mode) return;
        var state = DecodeState(await ReadStatusCoreAsync(token).ConfigureAwait(false));
        if (state is MotionDriveState.OperationEnabled or MotionDriveState.QuickStopActive or MotionDriveState.Fault or MotionDriveState.FaultReactionActive)
            throw new InvalidOperationException("切换模式前必须按设备要求停轴并去使能，故障态不能切换模式。");
        await WriteValueAsync(0x6060, 0, [unchecked((byte)(sbyte)mode)], token).ConfigureAwait(false);
        while (await ReadModeCoreAsync(token).ConfigureAwait(false) != (sbyte)mode)
        {
            ThrowIfFault(await ReadStatusCoreAsync(token).ConfigureAwait(false));
            await DelayAsync(token).ConfigureAwait(false);
        }
    }, timeoutMs, ct);

    public Task ResetFaultAsync(int timeoutMs, CancellationToken ct) => ControlAsync(async token =>
    {
        var initial = DecodeState(await ReadStatusCoreAsync(token).ConfigureAwait(false));
        if (initial is not (MotionDriveState.Fault or MotionDriveState.FaultReactionActive)) return;
        while (DecodeState(await ReadStatusCoreAsync(token).ConfigureAwait(false)) == MotionDriveState.FaultReactionActive)
            await DelayAsync(token).ConfigureAwait(false);
        await WriteControlAsync(0, token).ConfigureAwait(false);
        await WriteControlAsync(0x80, token).ConfigureAwait(false);
        while (DecodeState(await ReadStatusCoreAsync(token).ConfigureAwait(false)) is MotionDriveState.Fault or MotionDriveState.FaultReactionActive)
            await DelayAsync(token).ConfigureAwait(false);
        await WriteControlAsync(0, token).ConfigureAwait(false);
        _controlUncertain = false;
    }, timeoutMs, ct, recovery: true);

    public Task EnableAsync(int timeoutMs, CancellationToken ct) => ControlAsync(async token =>
    {
        var initialStatus = await ReadStatusCoreAsync(token).ConfigureAwait(false);
        ThrowIfFault(initialStatus);
        var state = DecodeState(initialStatus);
        if (state == MotionDriveState.OperationEnabled) return;
        if (state == MotionDriveState.QuickStopActive) throw new InvalidOperationException("轴处于快速停止状态，须明确去使能后再重新使能。");
        var mode = (MotionMode)await ReadModeCoreAsync(token).ConfigureAwait(false);
        RequireModeAllowed(mode);
        await PrepareInitialTargetAsync(mode, token).ConfigureAwait(false);
        while (true)
        {
            var status = await ReadStatusCoreAsync(token).ConfigureAwait(false);
            ThrowIfFault(status);
            switch (DecodeState(status))
            {
                case MotionDriveState.OperationEnabled: return;
                case MotionDriveState.SwitchOnDisabled: await WriteControlAsync(6, token).ConfigureAwait(false); break;
                case MotionDriveState.ReadyToSwitchOn: await WriteControlAsync(7, token).ConfigureAwait(false); break;
                case MotionDriveState.SwitchedOn: await WriteControlAsync(15, token).ConfigureAwait(false); break;
                case MotionDriveState.NotReady: break;
                default: throw new InvalidOperationException($"驱动状态不允许使能：0x{status:X4}。");
            }
            await DelayAsync(token).ConfigureAwait(false);
        }
    }, timeoutMs, ct);

    public Task DisableAsync(int timeoutMs, CancellationToken ct) => ControlAsync(async token =>
    {
        await WriteControlAsync(0, token).ConfigureAwait(false);
        await WaitStatusAsync(s => DecodeState(s) == MotionDriveState.SwitchOnDisabled, token).ConfigureAwait(false);
        _controlUncertain = false;
    }, timeoutMs, ct, recovery: true, interrupt: true);

    public Task UnbindAsync(int timeoutMs, CancellationToken ct) => RunAsync(async token =>
    {
        if (_hasWrittenControl)
        {
            var state = DecodeState(await ReadStatusCoreAsync(token).ConfigureAwait(false));
            if (state is MotionDriveState.OperationEnabled or MotionDriveState.QuickStopActive)
                throw new InvalidOperationException("本轴执行过控制且尚未去使能，请先停止运动并去使能后再解绑。");
        }
        token.ThrowIfCancellationRequested();
        _io.ReleaseAxis(AxisName);
        _released = true;
        return true;
    }, timeoutMs, ct);

    public static MotionDriveState DecodeState(ushort statusWord)
    {
        if ((statusWord & 0x004F) == 0x0000) return MotionDriveState.NotReady;
        if ((statusWord & 0x004F) == 0x0040) return MotionDriveState.SwitchOnDisabled;
        if ((statusWord & 0x004F) == 0x000F) return MotionDriveState.FaultReactionActive;
        if ((statusWord & 0x004F) == 0x0008) return MotionDriveState.Fault;
        return (statusWord & 0x006F) switch
        {
            0x0021 => MotionDriveState.ReadyToSwitchOn,
            0x0023 => MotionDriveState.SwitchedOn,
            0x0027 => MotionDriveState.OperationEnabled,
            0x0007 => MotionDriveState.QuickStopActive,
            _ => MotionDriveState.Unknown
        };
    }

    private async Task ControlAsync(Func<CancellationToken, Task> action, int timeoutMs, CancellationToken ct, bool recovery = false, bool interrupt = false)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.GetTimestamp();
        long epoch;
        Task? stoppingCallbacks = null;
        lock (_controlSync)
        {
            epoch = interrupt ? ++_controlEpoch : _controlEpoch;
            if (interrupt && _activeControl is not null) stoppingCallbacks = _activeControl.CancelAsync();
        }
        if (stoppingCallbacks is not null)
        {
            try { await stoppingCallbacks.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                _ = stoppingCallbacks.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw new TimeoutException($"轴 {AxisName} 停止等待取消回调超时，不能证明原调用已退出。");
            }
        }
        var remaining = timeoutMs - (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (remaining <= 0) throw new TimeoutException($"轴 {AxisName} 操作已超过统一等待期限。");
        await RunAsync(async token =>
        {
            using var controlCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            lock (_controlSync)
            {
                if (epoch != _controlEpoch) throw new InvalidOperationException("先前排队的控制请求因停止或恢复请求而作废，须显式重新提交。");
                _activeControl = controlCancellation;
            }
            try
            {
                if (_controlUncertain && !recovery) throw new InvalidOperationException("上次控制未确认完成；请检查设备并显式停止或去使能，不能继续提交运动。");
                await action(controlCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException error) when (!token.IsCancellationRequested && controlCancellation.IsCancellationRequested)
            {
                if (_hasWrittenControl) _controlUncertain = true;
                throw new InvalidOperationException("当前控制被显式停止或恢复请求中断；中断本身不证明设备已停止。", error);
            }
            catch { if (_hasWrittenControl) _controlUncertain = true; throw; }
            finally
            {
                lock (_controlSync)
                {
                    if (ReferenceEquals(_activeControl, controlCancellation)) _activeControl = null;
                }
            }
            return true;
        }, remaining, ct).ConfigureAwait(false);
    }

    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "超时必须大于零。");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeoutMs);
        var operationDeadline = Stopwatch.GetTimestamp() + (long)(timeoutMs * (double)Stopwatch.Frequency / 1000);
        var previousDeadline = _deadlineTimestamp.Value;
        var acquired = false;
        Task<T>? work = null;
        try
        {
            await _operation.WaitAsync(deadline.Token).ConfigureAwait(false);
            acquired = true;
            deadline.Token.ThrowIfCancellationRequested();
            if (_released) throw new ObjectDisposedException(AxisName, "轴已解绑。");
            _deadlineTimestamp.Value = operationDeadline;
            work = action(deadline.Token);
            return await work.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"轴 {AxisName} 操作超过 {timeoutMs} ms，未确认完成；不代表设备动作已撤回或停止。");
        }
        finally
        {
            _deadlineTimestamp.Value = previousDeadline;
            if (acquired)
            {
                if (work is { IsCompleted: false })
                {
                    // 软期限结束仍保留轴互斥；真实任务退出后才允许后续恢复或解绑。
                    _ = work.ContinueWith(t => { _ = t.Exception; _operation.Release(); }, CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                else _operation.Release();
            }
        }
    }

    private async Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, int length, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes = await _io.ReadObjectAsync(_configuration.SlaveIndex, index, subIndex, length, RemainingMs(), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (bytes.Length != length) throw new InvalidDataException($"轴 {AxisName} 对象 0x{index:X4}:{subIndex} 长度应为 {length}，实际 {bytes.Length}。");
        return bytes;
    }

    private async Task WriteValueAsync(ushort index, byte subIndex, byte[] bytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var offset = index switch
        {
            0x6040 => _configuration.ControlWordOffset,
            0x6060 => _configuration.ModeCommandOffset,
            0x607A => _configuration.TargetPositionOffset,
            0x60FF => _configuration.TargetVelocityOffset,
            0x6071 => _configuration.TargetTorqueOffset,
            _ => -1
        };
        _hasWrittenControl = true;
        if (offset >= 0) await _io.SubmitPdoAsync(AxisName, [new(offset, bytes)], RemainingMs(), ct).ConfigureAwait(false);
        else await _io.WriteObjectAsync(_configuration.SlaveIndex, index, subIndex, bytes, RemainingMs(), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }

    private Task WriteControlAsync(ushort value, CancellationToken ct) => WriteValueAsync(0x6040, 0, U16(value), ct);
    private int RemainingMs()
    {
        var remaining = (_deadlineTimestamp.Value - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
        if (remaining <= 0) throw new TimeoutException($"轴 {AxisName} 操作总期限已到，不再提交设备访问。");
        return Math.Max(1, (int)Math.Min(int.MaxValue, Math.Ceiling(remaining)));
    }
    private Task DelayAsync(CancellationToken ct) => Task.Delay(_configuration.PollIntervalMs, ct);
    private static byte[] U16(ushort value) { var bytes = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, value); return bytes; }
    private static byte[] I16(short value) { var bytes = new byte[2]; BinaryPrimitives.WriteInt16LittleEndian(bytes, value); return bytes; }
    private static byte[] I32(int value) { var bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, value); return bytes; }
    private static byte[] U32(uint value) { var bytes = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); return bytes; }

    private void RequireModeAllowed(MotionMode mode)
    {
        if (!Enum.IsDefined(mode) || !_configuration.AllowedModes.Contains(mode)) throw new InvalidOperationException($"轴 {AxisName} 未配置允许模式 {mode}。");
    }
    private static void ThrowIfFault(ushort status)
    {
        if (DecodeState(status) is MotionDriveState.Fault or MotionDriveState.FaultReactionActive)
            throw new InvalidOperationException($"驱动处于故障状态，状态字 0x{status:X4}。");
    }
    private async Task<ushort> WaitStatusAsync(Func<ushort, bool> predicate, CancellationToken ct)
    {
        while (true)
        {
            var status = await ReadStatusCoreAsync(ct).ConfigureAwait(false);
            ThrowIfFault(status);
            if (predicate(status)) return status;
            await DelayAsync(ct).ConfigureAwait(false);
        }
    }
}
