using CiA402.Models;
using CiA402.Transport;

namespace CiA402.Core;

public sealed class AxisController(ICia402AxisTransport transport, BindAxisSetting configuration)
{
    private readonly ICia402AxisTransport _transport = new CoordinatedAxisTransport(transport, configuration.CommunicationTimeoutMs);
    private readonly Dictionary<OperationMode, long> _cyclicTargets = new();
    private ushort _control;
    private OperationMode? _mode;
    public ICia402AxisTransport Transport => _transport;
    public int PollIntervalMs { get; set; } = 20;
    private ushort Index(int index) => checked((ushort)(index + configuration.ObjectIndexOffset));

    public static DriveState DecodeState(ushort status)
    {
        return (status & 0x004F) switch
        {
            0x0000 => DriveState.NotReadyToSwitchOn,
            0x0040 => DriveState.SwitchOnDisabled,
            0x000F => DriveState.FaultReactionActive,
            0x0008 => DriveState.Fault,
            _ => (status & 0x006F) switch
            {
                0x0021 => DriveState.ReadyToSwitchOn, 0x0023 => DriveState.SwitchedOn,
                0x0027 => DriveState.OperationEnabled, 0x0007 => DriveState.QuickStopActive,
                _ => DriveState.Unknown
            }
        };
    }

    public async Task<long> ReadAsync(int index, int bytes, bool signed, CancellationToken ct, byte subIndex = 0)
        => ObjectCodec.Decode(await _transport.ReadObjectAsync(Index(index), subIndex, ct).ConfigureAwait(false), bytes, signed);
    public Task WriteAsync(int index, long value, int bytes, bool signed, CancellationToken ct, byte subIndex = 0)
        => _transport.WriteObjectAsync(Index(index), subIndex, ObjectCodec.Encode(value, bytes, signed), ct);

    public async Task InitializeAsync(CancellationToken ct)
    {
        _control = (ushort)await ReadAsync(0x6040, 2, false, ct).ConfigureAwait(false);
        var state = DecodeState(await ReadStatusAsync(ct).ConfigureAwait(false));
        if (state is DriveState.OperationEnabled or DriveState.QuickStopActive)
            throw new InvalidOperationException("绑定轴前必须先释放使能，不能接管正在运行的轴");
    }

    public async Task<ushort> ReadStatusAsync(CancellationToken ct) => (ushort)await ReadAsync(0x6041, 2, false, ct).ConfigureAwait(false);
    public async Task<sbyte> ReadModeAsync(CancellationToken ct) => (sbyte)await ReadAsync(0x6061, 1, true, ct).ConfigureAwait(false);

    public async Task<long> ReadHardwareAsync(CancellationToken ct)
    {
        if (configuration.DiagnosticIndex == 0) throw new InvalidOperationException("未配置硬件诊断对象，不能推断硬件状态");
        return ObjectCodec.Decode(await _transport.ReadObjectAsync((ushort)configuration.DiagnosticIndex,
            (byte)configuration.DiagnosticSubIndex, ct).ConfigureAwait(false), configuration.DiagnosticBytes, false);
    }
    public bool HardwareActive(long value) => configuration.DiagnosticMask != 0 &&
        (value & configuration.DiagnosticMask) == configuration.DiagnosticActiveValue;
    private async Task CheckHardwareAsync(CancellationToken ct)
    {
        if (configuration.DiagnosticMask != 0 && HardwareActive(await ReadHardwareAsync(ct).ConfigureAwait(false)))
            throw new InvalidOperationException("驱动硬件条件不满足，已停止继续提交运动命令");
    }
    private static void CheckFault(ushort status)
    {
        var state = DecodeState(status);
        if (state is DriveState.Fault or DriveState.FaultReactionActive or DriveState.Unknown)
            throw new InvalidOperationException($"驱动状态为 {state}，状态字 0x{status:X4}，不能继续控制");
    }

    private async Task ControlAsync(ushort mask, ushort value, CancellationToken ct)
    {
        var next = (ushort)((_control & ~mask) | (value & mask));
        await WriteAsync(0x6040, next, 2, false, ct).ConfigureAwait(false);
        _control = next;
        // 将已提交的周期目标同步为新的控制字，防止后续 SYNC 应用旧的使能位。
        if (_mode is { } mode && _cyclicTargets.TryGetValue(mode, out var target))
            await _transport.WriteTargetAsync(mode, target, _control, ct).ConfigureAwait(false);
    }

    private async Task WaitAsync(Func<ushort, bool> reached, CancellationToken ct, bool allowFault = false, bool motion = false)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var status = await ReadStatusAsync(ct).ConfigureAwait(false);
            if (!allowFault) CheckFault(status);
            if (motion)
            {
                if (DecodeState(status) != DriveState.OperationEnabled) throw new InvalidOperationException($"运动期间驱动退出使能状态：0x{status:X4}");
                if ((status & 0x0800) != 0) throw new InvalidOperationException("驱动内部限幅已激活，运动未能按目标执行");
                if (_mode is OperationMode.PP or OperationMode.CSP && (status & 0x2000) != 0) throw new InvalidOperationException("驱动报告位置跟随错误");
                await CheckHardwareAsync(ct).ConfigureAwait(false);
            }
            if (reached(status)) return;
            await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    public async Task EnableAsync(CancellationToken ct)
    {
        await CheckHardwareAsync(ct).ConfigureAwait(false);
        var initialStatus = await ReadStatusAsync(ct).ConfigureAwait(false);
        CheckFault(initialStatus);
        if (DecodeState(initialStatus) == DriveState.OperationEnabled) return;
        var modeValue = await ReadModeAsync(ct).ConfigureAwait(false);
        if (!Enum.IsDefined(typeof(OperationMode), modeValue)) throw new InvalidOperationException("实际运行模式不在支持范围，请先设置模式");
        _mode = (OperationMode)modeValue;
        if (_mode is OperationMode.CSP or OperationMode.CSV or OperationMode.CST)
        {
            _transport.RequireCyclicMode(_mode.Value);
            if (!_cyclicTargets.TryGetValue(_mode.Value, out var initialTarget)) throw new InvalidOperationException("周期模式使能前必须先写入初始目标，并按设备要求完成 SYNC");
            var expected = _mode == OperationMode.CSP ? await ReadAsync(0x6064, 4, true, ct).ConfigureAwait(false) : 0;
            if (initialTarget != expected) throw new InvalidOperationException("使能前周期目标与当前位置或零目标不一致，请重新准备初始目标");
        }
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var status = await ReadStatusAsync(ct).ConfigureAwait(false);
            CheckFault(status);
            await CheckHardwareAsync(ct).ConfigureAwait(false);
            switch (DecodeState(status))
            {
                case DriveState.OperationEnabled: return;
                case DriveState.NotReadyToSwitchOn: break;
                case DriveState.SwitchOnDisabled: await ControlAsync(0x008F, 0x0006, ct).ConfigureAwait(false); break;
                case DriveState.ReadyToSwitchOn: await ControlAsync(0x008F, 0x0007, ct).ConfigureAwait(false); break;
                case DriveState.SwitchedOn: await ControlAsync(0x008F, 0x000F, ct).ConfigureAwait(false); break;
                default: throw new InvalidOperationException("当前状态不允许自动使能，请先停止运动并释放使能");
            }
            await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    public async Task DisableAsync(CancellationToken ct)
    {
        await ControlAsync(0x008F, 0, ct).ConfigureAwait(false);
        await WaitAsync(s => DecodeState(s) == DriveState.SwitchOnDisabled, ct).ConfigureAwait(false);
    }

    public async Task ResetFaultAsync(CancellationToken ct)
    {
        var state = DecodeState(await ReadStatusAsync(ct).ConfigureAwait(false));
        if (state == DriveState.FaultReactionActive)
            await WaitAsync(s => DecodeState(s) == DriveState.Fault, ct, allowFault: true).ConfigureAwait(false);
        else if (state != DriveState.Fault) throw new InvalidOperationException("故障复位仅允许在 Fault 状态执行");
        await ControlAsync(0x008F, 0, ct).ConfigureAwait(false);
        try
        {
            await ControlAsync(0x008F, 0x0080, ct).ConfigureAwait(false);
            await WaitAsync(s => DecodeState(s) is DriveState.SwitchOnDisabled or DriveState.ReadyToSwitchOn or DriveState.SwitchedOn, ct, allowFault: true).ConfigureAwait(false);
        }
        finally
        {
            // 正常路径撤销复位位；异常路径由会话清理撤销，不能沿用已取消令牌写入。
            if (!ct.IsCancellationRequested) await ControlAsync(0x008F, 0, ct).ConfigureAwait(false);
        }
    }

    public Task SetModeAsync(OperationMode mode, CancellationToken ct) => SetModeAsync(mode, false, ct);

    public async Task SetModeAsync(OperationMode mode, bool allowWhileEnabled, CancellationToken ct)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("运行模式无效");
        var state = DecodeState(await ReadStatusAsync(ct).ConfigureAwait(false));
        if (state == DriveState.OperationEnabled && allowWhileEnabled)
        {
            await SetEnabledModeAsync(mode, ct).ConfigureAwait(false);
            return;
        }
        if (state is not (DriveState.SwitchOnDisabled or DriveState.ReadyToSwitchOn or DriveState.SwitchedOn))
            throw new InvalidOperationException("切换模式前必须释放运行使能，并确认驱动无故障");
        if (mode is OperationMode.CSP or OperationMode.CSV or OperationMode.CST) _transport.RequireCyclicMode(mode);
        await ControlAsync(0x01F0, 0, ct).ConfigureAwait(false);
        await WriteAsync(0x6060, (sbyte)mode, 1, true, ct).ConfigureAwait(false);
        while (await ReadModeAsync(ct).ConfigureAwait(false) != (sbyte)mode)
        {
            CheckFault(await ReadStatusAsync(ct).ConfigureAwait(false));
            await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
        }
        _mode = mode;
        _cyclicTargets.Clear();
    }

    private async Task CheckEnabledSwitchAsync(CancellationToken ct)
    {
        var status = await ReadStatusAsync(ct).ConfigureAwait(false);
        CheckFault(status);
        if (DecodeState(status) != DriveState.OperationEnabled)
            throw new InvalidOperationException("模式切换期间驱动退出运行使能状态，不能视为使能态切换成功");
        if (await ReadAsync(0x606C, 4, true, ct).ConfigureAwait(false) != 0)
            throw new InvalidOperationException("使能态切换要求实际速度为零，请先停止运动；本步骤不会自动停轴");
        await CheckHardwareAsync(ct).ConfigureAwait(false);
    }

    private async Task SetEnabledModeAsync(OperationMode mode, CancellationToken ct)
    {
        var currentValue = await ReadModeAsync(ct).ConfigureAwait(false);
        if (!Enum.IsDefined(typeof(OperationMode), currentValue)) throw new InvalidOperationException("当前实际模式不在支持范围，不能执行使能态切换");
        var current = (OperationMode)currentValue;
        await CheckEnabledSwitchAsync(ct).ConfigureAwait(false);
        if (current == mode) return;
        if (mode is OperationMode.CSP or OperationMode.CSV or OperationMode.CST) _transport.RequireCyclicMode(mode);

        _control = (ushort)await ReadAsync(0x6040, 2, false, ct).ConfigureAwait(false);
        if ((_control & 0x0190) != 0)
            throw new InvalidOperationException("使能态切换前必须撤销运动启动、复位和 Halt 标记；本步骤不隐式恢复运动");
        var status = await ReadStatusAsync(ct).ConfigureAwait(false);
        if ((status & 0x0800) != 0 || (current is OperationMode.PP or OperationMode.CSP or OperationMode.HM && (status & 0x2000) != 0))
            throw new InvalidOperationException("驱动报告限幅或模式错误，不能切换模式");
        if (current == OperationMode.PP && (status & 0x0400) == 0)
            throw new InvalidOperationException("PP 运动尚未确认到位，不能执行使能态切换");
        var position = await ReadAsync(0x6064, 4, true, ct).ConfigureAwait(false);
        if (current is OperationMode.PV or OperationMode.CSV && await ReadAsync(0x60FF, 4, true, ct).ConfigureAwait(false) != 0)
            throw new InvalidOperationException("切换前速度模式目标必须为零");
        if (current is OperationMode.PT or OperationMode.CST && await ReadAsync(0x6071, 2, true, ct).ConfigureAwait(false) != 0)
            throw new InvalidOperationException("切换前转矩模式目标必须为零");
        if (current == OperationMode.CSP && await ReadAsync(0x607A, 4, true, ct).ConfigureAwait(false) != position)
            throw new InvalidOperationException("切换前 CSP 目标必须等于当前位置");
        if (_cyclicTargets.TryGetValue(current, out var previousTarget) && previousTarget != (current == OperationMode.CSP ? position : 0))
            throw new InvalidOperationException("尚有非静止周期目标，请先停止更新并完成上一周期 PDO/SYNC");

        // 只撤销旧模式的目标选项，不改变使能位，不重新排队旧模式的 PDO。
        var nextControl = (ushort)(_control & ~0x0070);
        if (nextControl != _control)
        {
            await WriteAsync(0x6040, nextControl, 2, false, ct).ConfigureAwait(false);
            _control = nextControl;
        }
        var initialTarget = mode is OperationMode.PP or OperationMode.CSP ? position : 0;
        switch (mode)
        {
            case OperationMode.PP or OperationMode.CSP:
                await WriteAsync(0x607A, initialTarget, 4, true, ct).ConfigureAwait(false); break;
            case OperationMode.PV or OperationMode.CSV:
                await WriteAsync(0x60FF, 0, 4, true, ct).ConfigureAwait(false); break;
            case OperationMode.PT or OperationMode.CST:
                await WriteAsync(0x6071, 0, 2, true, ct).ConfigureAwait(false); break;
            case OperationMode.HM: break;
        }
        await CheckEnabledSwitchAsync(ct).ConfigureAwait(false);
        if (mode is OperationMode.PP or OperationMode.CSP && await ReadAsync(0x6064, 4, true, ct).ConfigureAwait(false) != initialTarget)
            throw new InvalidOperationException("准备初始位置期间实际位置发生变化，不能继续切换");
        await WriteAsync(0x6060, (sbyte)mode, 1, true, ct).ConfigureAwait(false);
        while (true)
        {
            var displayed = await ReadModeAsync(ct).ConfigureAwait(false);
            await CheckEnabledSwitchAsync(ct).ConfigureAwait(false);
            if (displayed == (sbyte)mode) break;
            await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
        }
        _mode = mode;
        _cyclicTargets.Clear();
        if (mode is OperationMode.CSP or OperationMode.CSV or OperationMode.CST)
        {
            _cyclicTargets[mode] = initialTarget;
            await _transport.WriteTargetAsync(mode, initialTarget, _control, ct).ConfigureAwait(false);
        }
    }

    private async Task RequireMotionAsync(OperationMode mode, CancellationToken ct, bool allowDisabled = false)
    {
        var status = await ReadStatusAsync(ct).ConfigureAwait(false);
        CheckFault(status);
        var state = DecodeState(status);
        if (state != DriveState.OperationEnabled && !(allowDisabled && state is DriveState.SwitchOnDisabled or DriveState.ReadyToSwitchOn or DriveState.SwitchedOn))
            throw new InvalidOperationException("驱动当前状态不允许提交运动目标");
        if (await ReadModeAsync(ct).ConfigureAwait(false) != (sbyte)mode) throw new InvalidOperationException($"当前实际模式不是 {mode}，请先设置模式");
        if ((status & 0x0800) != 0) throw new InvalidOperationException("驱动内部限幅已激活");
        if (mode is OperationMode.PP or OperationMode.CSP && (status & 0x2000) != 0) throw new InvalidOperationException("驱动报告位置跟随错误");
        await CheckHardwareAsync(ct).ConfigureAwait(false);
        _mode = mode;
    }

    public async Task MovePositionAsync(long target, long velocity, long acceleration, long deceleration, bool relative, bool wait, CancellationToken ct)
    {
        await RequireMotionAsync(OperationMode.PP, ct).ConfigureAwait(false);
        await ControlAsync(0x0170, 0, ct).ConfigureAwait(false);
        await WaitAsync(s => (s & 0x1000) == 0, ct, motion: true).ConfigureAwait(false);
        await WriteAsync(0x6081, velocity, 4, false, ct).ConfigureAwait(false);
        await WriteAsync(0x6083, acceleration, 4, false, ct).ConfigureAwait(false);
        await WriteAsync(0x6084, deceleration, 4, false, ct).ConfigureAwait(false);
        await WriteAsync(0x607A, target, 4, true, ct).ConfigureAwait(false);
        await ControlAsync(0x0170, (ushort)(0x0030 | (relative ? 0x0040 : 0)), ct).ConfigureAwait(false);
        await WaitAsync(s => (s & 0x1000) != 0, ct, motion: true).ConfigureAwait(false);
        await ControlAsync(0x0010, 0, ct).ConfigureAwait(false);
        await WaitAsync(s => (s & 0x1000) == 0, ct, motion: true).ConfigureAwait(false);
        if (wait) await WaitAsync(s => (s & 0x0400) != 0, ct, motion: true).ConfigureAwait(false);
    }

    public async Task RunVelocityAsync(long target, long acceleration, long deceleration, CancellationToken ct)
    {
        await RequireMotionAsync(OperationMode.PV, ct).ConfigureAwait(false);
        await WriteAsync(0x6083, acceleration, 4, false, ct).ConfigureAwait(false);
        await WriteAsync(0x6084, deceleration, 4, false, ct).ConfigureAwait(false);
        await WriteAsync(0x60FF, target, 4, true, ct).ConfigureAwait(false);
        await ControlAsync(0x0100, 0, ct).ConfigureAwait(false);
    }

    public async Task RunTorqueAsync(long target, long slope, CancellationToken ct)
    {
        await RequireMotionAsync(OperationMode.PT, ct).ConfigureAwait(false);
        await WriteAsync(0x6087, slope, 4, false, ct).ConfigureAwait(false);
        await WriteAsync(0x6071, target, 2, true, ct).ConfigureAwait(false);
        await ControlAsync(0x0100, 0, ct).ConfigureAwait(false);
    }

    public async Task HomeAsync(long method, long search, long zero, long acceleration, CancellationToken ct)
    {
        await RequireMotionAsync(OperationMode.HM, ct).ConfigureAwait(false);
        if (method == 0) throw new ArgumentException("回零方式 0 不可用于启动回零");
        await ControlAsync(0x0110, 0, ct).ConfigureAwait(false);
        await WriteAsync(0x6098, method, 1, true, ct).ConfigureAwait(false);
        await WriteAsync(0x6099, search, 4, false, ct, 1).ConfigureAwait(false);
        await WriteAsync(0x6099, zero, 4, false, ct, 2).ConfigureAwait(false);
        await WriteAsync(0x609A, acceleration, 4, false, ct).ConfigureAwait(false);
        await WaitAsync(s => (s & 0x1000) == 0, ct, motion: true).ConfigureAwait(false);
        await ControlAsync(0x0010, 0x0010, ct).ConfigureAwait(false);
        await WaitAsync(s =>
        {
            if ((s & 0x2000) != 0) throw new InvalidOperationException("驱动报告回零错误");
            return (s & 0x1400) == 0x1400;
        }, ct, motion: true).ConfigureAwait(false);
        await ControlAsync(0x0010, 0, ct).ConfigureAwait(false);
    }

    public async Task WriteCyclicAsync(OperationMode mode, long target, CancellationToken ct)
    {
        await RequireMotionAsync(mode, ct, allowDisabled: true).ConfigureAwait(false);
        _transport.RequireCyclicMode(mode);
        if (!_cyclicTargets.ContainsKey(mode))
        {
            var initial = mode == OperationMode.CSP ? await ReadAsync(0x6064, 4, true, ct).ConfigureAwait(false) : 0;
            if (target != initial) throw new InvalidOperationException("首次周期目标必须为当前位置（CSP）或零（CSV/CST），避免使能时目标突变");
        }
        await _transport.WriteTargetAsync(mode, target, _control, ct).ConfigureAwait(false);
        _cyclicTargets[mode] = target;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        var state = DecodeState(await ReadStatusAsync(ct).ConfigureAwait(false));
        if (state == DriveState.OperationEnabled)
        {
            var option = await ReadAsync(0x605A, 2, true, ct).ConfigureAwait(false);
            if (option is < 1 or > 8) throw new InvalidOperationException("驱动停止选项不提供受支持的减速停止方式，请先按设备手册配置 0x605A");
            await ControlAsync(0x008F, 0x0002, ct).ConfigureAwait(false);
        }
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var status = await ReadStatusAsync(ct).ConfigureAwait(false);
            CheckFault(status);
            if (await ReadAsync(0x606C, 4, true, ct).ConfigureAwait(false) == 0 && DecodeState(status) != DriveState.OperationEnabled) return;
            await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    public async Task CleanupAsync(Action<string, Exception?> log)
    {
        using var timeout = new CancellationTokenSource(configuration.CommunicationTimeoutMs);
        try { await ControlAsync(0x008F, 0x0002, timeout.Token).ConfigureAwait(false); }
        catch (Exception ex) { log("尝试停止运动失败", ex); }
        using var releaseTimeout = new CancellationTokenSource(configuration.CommunicationTimeoutMs);
        try { await ControlAsync(0x008F, 0, releaseTimeout.Token).ConfigureAwait(false); }
        catch (Exception ex) { log("尝试释放使能失败", ex); }
    }
}
