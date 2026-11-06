namespace ZlgEtherCat.Motion;

public sealed partial class ZlgMotionAxis
{
    /// <summary>PP 目标使用驱动原始位置单位；确认新目标握手和目标到达。</summary>
    public Task MovePositionAsync(int position, bool relative, uint profileVelocity, uint acceleration, uint deceleration, int timeoutMs, CancellationToken ct)
        => ControlAsync(async token =>
        {
            RequirePositive(profileVelocity, "轮廓速度");
            RequirePositive(acceleration, "加速度");
            RequirePositive(deceleration, "减速度");
            await RequireEnabledModeAsync(MotionMode.PP, token).ConfigureAwait(false);
            var baseControl = (ushort)(0x000F | (relative ? 0x0040 : 0));
            await WriteControlAsync(baseControl, token).ConfigureAwait(false);
            await WaitStatusAsync(s => (s & 0x1000) == 0, token).ConfigureAwait(false);
            await WriteValueAsync(0x6081, 0, U32(profileVelocity), token).ConfigureAwait(false);
            await WriteValueAsync(0x6083, 0, U32(acceleration), token).ConfigureAwait(false);
            await WriteValueAsync(0x6084, 0, U32(deceleration), token).ConfigureAwait(false);
            await WriteValueAsync(0x607A, 0, I32(position), token).ConfigureAwait(false);
            await WriteControlAsync((ushort)(baseControl | 0x0010), token).ConfigureAwait(false);
            await WaitStatusAsync(s => (s & 0x1000) != 0, token).ConfigureAwait(false);
            await WriteControlAsync(baseControl, token).ConfigureAwait(false);
            await WaitStatusAsync(s => (s & 0x1000) == 0, token).ConfigureAwait(false);
            await WaitStatusAsync(s => (s & 0x0400) != 0 && DecodeState(s) == MotionDriveState.OperationEnabled, token).ConfigureAwait(false);
        }, timeoutMs, ct);

    /// <summary>PV 提交持续速度，返回不代表运动结束；需显式 StopMotion。</summary>
    public Task RunVelocityAsync(int velocity, uint acceleration, uint deceleration, int timeoutMs, CancellationToken ct)
        => ControlAsync(async token =>
        {
            RequirePositive(acceleration, "加速度");
            RequirePositive(deceleration, "减速度");
            await RequireEnabledModeAsync(MotionMode.PV, token).ConfigureAwait(false);
            await WriteValueAsync(0x6083, 0, U32(acceleration), token).ConfigureAwait(false);
            await WriteValueAsync(0x6084, 0, U32(deceleration), token).ConfigureAwait(false);
            await WriteValueAsync(0x60FF, 0, I32(velocity), token).ConfigureAwait(false);
            await WriteControlAsync(0x000F, token).ConfigureAwait(false);
        }, timeoutMs, ct);

    /// <summary>PT 目标使用驱动原始转矩单位，不能推断其为 N·m。</summary>
    public Task RunTorqueAsync(short torque, int timeoutMs, CancellationToken ct) => ControlAsync(async token =>
    {
        await RequireEnabledModeAsync(MotionMode.PT, token).ConfigureAwait(false);
        await WriteValueAsync(0x6071, 0, I16(torque), token).ConfigureAwait(false);
        await WriteControlAsync(0x000F, token).ConfigureAwait(false);
    }, timeoutMs, ct);

    public Task HomeAsync(sbyte method, uint searchVelocity, uint zeroVelocity, uint acceleration, int timeoutMs, CancellationToken ct)
        => ControlAsync(async token =>
        {
            if (method == 0) throw new ArgumentException("回零方法须按驱动手册显式配置，不能为 0。");
            RequirePositive(searchVelocity, "回零搜索速度");
            RequirePositive(zeroVelocity, "回零零位速度");
            RequirePositive(acceleration, "回零加速度");
            await RequireEnabledModeAsync(MotionMode.HM, token).ConfigureAwait(false);
            await WriteControlAsync(0x000F, token).ConfigureAwait(false);
            await WriteValueAsync(0x6098, 0, [unchecked((byte)method)], token).ConfigureAwait(false);
            await WriteValueAsync(0x6099, 1, U32(searchVelocity), token).ConfigureAwait(false);
            await WriteValueAsync(0x6099, 2, U32(zeroVelocity), token).ConfigureAwait(false);
            await WriteValueAsync(0x609A, 0, U32(acceleration), token).ConfigureAwait(false);
            await WriteControlAsync(0x001F, token).ConfigureAwait(false);
            // 防止沿用上次回零完成位：新启动后必须先观察到非完成状态。
            var observedActive = false;
            while (true)
            {
                var status = await ReadStatusCoreAsync(token).ConfigureAwait(false);
                ThrowIfFault(status);
                if ((status & 0x2000) != 0) throw new InvalidOperationException($"驱动报告回零失败，状态字 0x{status:X4}。");
                if ((status & 0x1400) != 0x1400) observedActive = true;
                if (observedActive && (status & 0x1400) == 0x1400) break;
                await DelayAsync(token).ConfigureAwait(false);
            }
            await WriteControlAsync(0x000F, token).ConfigureAwait(false);
        }, timeoutMs, ct);

    /// <summary>工艺停止：确认驱动状态和实际速度；不构成功能安全停轴证明。</summary>
    public Task StopMotionAsync(bool quickStop, int velocityTolerance, int timeoutMs, CancellationToken ct)
    {
        if (velocityTolerance < 0) throw new ArgumentOutOfRangeException(nameof(velocityTolerance), "停止速度容差不能为负。");
        return ControlAsync(async token =>
        {
            var status = await ReadStatusCoreAsync(token).ConfigureAwait(false);
            ThrowIfFault(status);
            var mode = (MotionMode)await ReadModeCoreAsync(token).ConfigureAwait(false);
            RequireModeAllowed(mode);
            if (!quickStop && mode is MotionMode.CSP or MotionMode.CSV or MotionMode.CST)
                throw new InvalidOperationException("周期同步模式不假定支持 Halt；请按驱动手册显式选择快速停止并确认其停止选项。");
            if (mode is MotionMode.PV or MotionMode.CSV) await WriteValueAsync(0x60FF, 0, I32(0), token).ConfigureAwait(false);
            if (mode is MotionMode.PT or MotionMode.CST) await WriteValueAsync(0x6071, 0, I16(0), token).ConfigureAwait(false);
            if (DecodeState(status) == MotionDriveState.OperationEnabled)
                await WriteControlAsync(quickStop ? (ushort)0x000B : (ushort)0x010F, token).ConfigureAwait(false);
            if (quickStop)
                await WaitStatusAsync(s => DecodeState(s) is MotionDriveState.QuickStopActive or MotionDriveState.SwitchOnDisabled, token).ConfigureAwait(false);
            var stable = 0;
            long previousSequence = -1;
            while (stable < 2)
            {
                ThrowIfFault(await ReadStatusCoreAsync(token).ConfigureAwait(false));
                var feedback = await ReadVelocityCoreAsync(token).ConfigureAwait(false);
                if (feedback.Sequence == 0 || feedback.Sequence != previousSequence)
                {
                    stable = Math.Abs((long)feedback.Velocity) <= velocityTolerance ? stable + 1 : 0;
                    previousSequence = feedback.Sequence;
                }
                if (stable < 2) await DelayAsync(token).ConfigureAwait(false);
            }
            // 已确认工艺停止允许显式后续操作，驱动使能状态保持其实际值。
            _controlUncertain = false;
        }, timeoutMs, ct, recovery: true, interrupt: true);
    }

    /// <summary>单次 CSP/CSV/CST 更新，仅原子提交目标与控制字；由通道固定输出循环消费。</summary>
    public Task WriteCyclicTargetAsync(MotionMode mode, long target, int timeoutMs, CancellationToken ct)
        => ControlAsync(async token =>
        {
            RequireCyclicMapping(mode);
            await RequireEnabledModeAsync(mode, token).ConfigureAwait(false);
            var offset = mode switch { MotionMode.CSP => _configuration.TargetPositionOffset, MotionMode.CSV => _configuration.TargetVelocityOffset, _ => _configuration.TargetTorqueOffset };
            var data = mode == MotionMode.CST ? I16(checked((short)target)) : I32(checked((int)target));
            token.ThrowIfCancellationRequested();
            _hasWrittenControl = true;
            await _io.SubmitPdoAsync(AxisName, [new(offset, data), new(_configuration.ControlWordOffset, U16(0x000F))], RemainingMs(), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }, timeoutMs, ct);

    private async Task RequireEnabledModeAsync(MotionMode mode, CancellationToken ct)
    {
        RequireModeAllowed(mode);
        var status = await ReadStatusCoreAsync(ct).ConfigureAwait(false);
        ThrowIfFault(status);
        if (DecodeState(status) != MotionDriveState.OperationEnabled) throw new InvalidOperationException("轴未处于运行使能状态。");
        if (await ReadModeCoreAsync(ct).ConfigureAwait(false) != (sbyte)mode)
            throw new InvalidOperationException($"驱动实际模式不是 {mode}，请先显式切换模式。");
    }

    private async Task PrepareInitialTargetAsync(MotionMode mode, CancellationToken ct)
    {
        if (mode is MotionMode.CSP or MotionMode.CSV or MotionMode.CST) RequireCyclicMapping(mode);
        if (mode == MotionMode.CSP) await WriteValueAsync(0x607A, 0, I32(await ReadPositionCoreAsync(ct).ConfigureAwait(false)), ct).ConfigureAwait(false);
        if (mode is MotionMode.PV or MotionMode.CSV) await WriteValueAsync(0x60FF, 0, I32(0), ct).ConfigureAwait(false);
        if (mode is MotionMode.PT or MotionMode.CST) await WriteValueAsync(0x6071, 0, I16(0), ct).ConfigureAwait(false);
    }

    private void RequireCyclicMapping(MotionMode mode)
    {
        RequireModeAllowed(mode);
        var validTarget = mode switch
        {
            MotionMode.CSP => _configuration.TargetPositionOffset >= 0 && _configuration.ActualPositionOffset >= 0,
            MotionMode.CSV => _configuration.TargetVelocityOffset >= 0 && _configuration.ActualVelocityOffset >= 0,
            MotionMode.CST => _configuration.TargetTorqueOffset >= 0 && _configuration.ActualTorqueOffset >= 0,
            _ => false
        };
        if (!validTarget || _configuration.ControlWordOffset < 0 || _configuration.StatusWordOffset < 0)
            throw new InvalidOperationException("周期运动须配置对应模式的目标、反馈、控制字和状态字 PDO，不能自动回退为 SDO 周期控制。");
    }

    private static void RequirePositive(uint value, string name)
    {
        if (value == 0) throw new ArgumentOutOfRangeException(name, $"{name}必须大于零，单位按驱动手册配置。");
    }
}
