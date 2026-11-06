using System.Diagnostics;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;
using ZlgEtherCat.Native;

namespace ZlgEtherCat.Runtime;

public sealed record ZlgDiagnosticResult(MasterDiagnostics Raw, bool Updated,
    bool DcInternalSyncEstablished, bool DcExternalSyncEstablished, bool DcPropagationDelayInitialized);

public sealed record SlavePiRanges(PiRange Input, PiRange Output);
public sealed record SlaveDiscoveryResult(ushort Index, ushort SlaveIndex, bool Active, bool Ready,
    ushort FixedAddress, SlaveDetails? Info, SlavePiRanges Range, string InfoSource, string FixedAddressSource);
public sealed record SlaveInfoResult(uint? VendorId, uint? ProductCode, uint? SerialNumber, uint? Revision,
    uint? Alias, uint? InputBits, uint? OutputBits, ushort SlaveIndex, bool Active, bool Ready,
    ushort FixedAddress, string InfoSource, string FixedAddressSource);
public sealed record SlaveStatusResult(byte? State, ushort? AlStatus, ushort? DlStatus,
    ushort SlaveIndex, bool Active, bool Ready, ushort FixedAddress, string FixedAddressSource);

/// <summary>由一个通道的唯一 SDK 工作线程拥有，不能跨连接共享。记录插件实际提交成功的锁存配置。</summary>
public sealed class ZlgSdkOperationState
{
    internal bool? LatchContinuous { get; set; }
    internal uint? LatchCaptureCount { get; set; }
    internal bool LatchStarted { get; set; }
    internal void ClearLatchCapture() { LatchStarted = false; LatchCaptureCount = null; }
    internal void ClearLatchConfiguration() { LatchContinuous = null; ClearLatchCapture(); }
}

/// <summary>只由连接的 SDK 工作线程调用。操作名来自固定 Executor，不接受用户指定原生入口。</summary>
public static class ZlgSdkOperations
{
    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
    {
        "ReadSdo", "WriteSdo", "EnumerateSlaves", "ReadSlaveInfo", "ReadSlaveStatus", "ReadDiagnostics",
        "ReadLinkDiagnostics", "ReadLastError", "ClearSlaveError", "ConfigureAutoClear", "ConfigureReadyLost",
        "ConfigureWatchdog", "ReadWatchdog", "EnableWatchdog", "DisableWatchdog", "FeedWatchdog",
        "ConfigureForceOutput", "ReadForceOutput", "DisableForceOutput", "FoeRead", "FoeWrite",
        "ReadSlaveMemory", "WriteSlaveMemory", "ReadSlaveEeprom", "WriteSlaveEeprom", "ResetChannel",
        "StartCapture", "ReadCapture", "StopCapture", "ReadBoardDi", "ReadBoardDo", "WriteBoardDo",
        "StartBoardPwm", "StopBoardPwm", "ReadBoardPwm", "ConfigureBoardPulse", "OutputBoardPulse",
        "StopBoardPulse", "ReadBoardPulse", "ConfigureBoardLatch", "StartBoardLatch", "StopBoardLatch",
        "ReadBoardLatch", "ResetBoardLatchCounter", "ResetBoard", "BoardFileRead", "BoardFileWrite", "BoardUpdate"
    };

    public static bool Handles(string op) => op is not null && Operations.Contains(op);

    private const string IdentitySource = "EcatGetSlaveInfo 返回的 SDK 身份信息；未独立校验在线设备身份";
    private const string OfflineIdentitySource = "从站离线，未读取在线身份信息";
    private const string AddressSource = "当前 ENI 的固定站地址；不是从 0 开始的位置索引或别名";

    public static IReadOnlyList<SlaveDiscoveryResult> DescribeSlaves(IZlgNativeApi api, IntPtr handle,
        Func<ushort, (PiRange Input, PiRange Output)>? rangeResolver = null, CancellationToken ct = default,
        Action? beforeCall = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        void Before() { ct.ThrowIfCancellationRequested(); beforeCall?.Invoke(); }
        Before(); var count = api.GetSlaveCount(handle);
        var result = new List<SlaveDiscoveryResult>(count);
        for (ushort slave = 0; slave < count; slave++)
        {
            var location = DescribeLocation(api, handle, slave, Before);
            SlaveDetails? info = null;
            if (location.Active) { Before(); info = api.GetSlaveInfo(handle, slave); }
            (PiRange Input, PiRange Output) ranges;
            Before();
            if (rangeResolver is not null) ranges = rangeResolver(slave);
            else
            {
                var input = api.GetSlavePiRange(handle, PiArea.LocalInput, slave);
                Before(); var output = api.GetSlavePiRange(handle, PiArea.LocalOutput, slave);
                ranges = (input, output);
            }
            result.Add(new(slave, slave, location.Active, location.Ready, location.FixedAddress, info,
                new(ranges.Input, ranges.Output), info is null ? OfflineIdentitySource : IdentitySource, AddressSource));
        }
        Before(); return result;
    }

    public static SlaveInfoResult DescribeSlaveInfo(IZlgNativeApi api, IntPtr handle, ushort slave,
        CancellationToken ct = default, Action? beforeCall = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        void Before() { ct.ThrowIfCancellationRequested(); beforeCall?.Invoke(); }
        ValidateSlave(api, handle, slave, Before);
        var location = DescribeLocation(api, handle, slave, Before);
        SlaveDetails? info = null;
        if (location.Active) { Before(); info = api.GetSlaveInfo(handle, slave); }
        Before();
        return new(info?.VendorId, info?.ProductCode, info?.SerialNumber, info?.Revision, info?.Alias,
            info?.InputBits, info?.OutputBits, slave, location.Active, location.Ready, location.FixedAddress,
            info is null ? OfflineIdentitySource : IdentitySource, AddressSource);
    }

    public static SlaveStatusResult DescribeSlaveStatus(IZlgNativeApi api, IntPtr handle, ushort slave,
        CancellationToken ct = default, Action? beforeCall = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        void Before() { ct.ThrowIfCancellationRequested(); beforeCall?.Invoke(); }
        ValidateSlave(api, handle, slave, Before);
        var location = DescribeLocation(api, handle, slave, Before);
        byte? state = null; ushort? alStatus = null, dlStatus = null;
        if (location.Active)
        {
            Before(); state = api.GetSlaveState(handle, slave);
            Before(); alStatus = api.GetSlaveAlStatus(handle, slave);
            Before(); dlStatus = api.GetSlaveDlStatus(handle, slave);
        }
        Before(); return new(state, alStatus, dlStatus, slave, location.Active, location.Ready, location.FixedAddress, AddressSource);
    }

    private static void ValidateSlave(IZlgNativeApi api, IntPtr handle, ushort slave, Action beforeCall)
    {
        beforeCall(); var count = api.GetSlaveCount(handle);
        if (slave >= count) throw new ArgumentOutOfRangeException(nameof(slave), $"从站位置索引 {slave} 超出当前 ENI 的从站数量 {count}；索引从 0 开始，不是固定站地址。");
    }

    private static (bool Active, bool Ready, ushort FixedAddress) DescribeLocation(IZlgNativeApi api,
        IntPtr handle, ushort slave, Action beforeCall)
    {
        beforeCall(); var address = api.GetSlaveFixedAddress(handle, slave);
        beforeCall(); var active = api.IsSlaveActive(handle, slave);
        bool ready = false;
        if (active) { beforeCall(); ready = api.IsSlaveReady(handle, slave); }
        return (active, ready, address);
    }

    /// <summary>根运行时在进入前负责资源所有权、无轴/无输出维护门禁及设置剩余 TimeoutMs。</summary>
    public static object? Execute(string op, ZlgRequest r, IZlgNativeApi api, IntPtr handle, BoardDetails board,
        CancellationToken ct = default, ZlgSdkOperationState? operationState = null)
    {
        if (!Handles(op)) throw new ArgumentException($"不支持的 SDK 操作：{op}", nameof(op));
        ArgumentNullException.ThrowIfNull(r); ArgumentNullException.ThrowIfNull(api); ArgumentNullException.ThrowIfNull(board);
        if (r.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(r.TimeoutMs));
        ct.ThrowIfCancellationRequested();
        // 官方 HPCIe-2E 型号表与通用 SDK 看门狗导出存在能力冲突，不能把导出存在当作型号支持。
        if (board.Name.StartsWith("HPCIe-2E", StringComparison.OrdinalIgnoreCase)
            && op is "ConfigureWatchdog" or "ReadWatchdog" or "EnableWatchdog" or "DisableWatchdog" or "FeedWatchdog")
            throw new NotSupportedException($"板卡型号 {board.Name} 的官方能力表标注 PDO 看门狗不支持；当前 SDK 导出不足以确认此型号及固件能力，因此拒绝看门狗操作。请先取得厂商兼容确认。");
        var timer = Stopwatch.StartNew();
        uint Remaining()
        {
            ct.ThrowIfCancellationRequested();
            var value = r.TimeoutMs - timer.Elapsed.TotalMilliseconds;
            if (value <= 0) throw new TimeoutException($"ZLG 操作 {op} 超过 {r.TimeoutMs} 毫秒，停止后续请求。");
            return checked((uint)Math.Max(1, Math.Floor(value)));
        }
        T Call<T>(Func<T> action) { Remaining(); return action(); }
        void Run(Action action) { Remaining(); action(); }
        uint U(string name) => checked((uint)r.Get<int>(name));
        ushort Slave() => checked((ushort)r.Get<int>("SlaveIndex"));
        void RequireStopped()
        { if (Call(() => api.IsBusRunning(handle))) throw new InvalidOperationException("此维护操作要求主站总线已停止；不得在活动通信中复位或更新板卡。"); }
        void RequireMailboxMaintenance()
        {
            if (!Call(() => api.IsBusRunning(handle))) throw new InvalidOperationException("从站维护需要已启动的主站，以执行邮箱或 ESC 访问。");
            var state = Call(() => api.GetMasterState(handle));
            if (state is not (2 or 3)) throw new InvalidOperationException("从站维护仅允许 PREOP 或 BOOT 状态，实际设备还须支持相应操作。");
            if (Call(() => api.IsPiEnabled(handle))) throw new InvalidOperationException("从站维护要求先禁用过程映像交换。");
        }
        ZlgSdkOperationState LatchState() => operationState
            ?? throw new InvalidOperationException("锁存操作缺少本连接的配置状态，不能推断硬件已配置或已触发。");
        IntPtr BoardHandle(bool requireIo = true)
        {
            if (requireIo && !board.Name.Equals("PCIe-1E16", StringComparison.OrdinalIgnoreCase)
                && !board.Name.Equals("PCIe-1E16-SM", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"板卡 {board.Name} 未核实板载 I/O、脉冲、PWM 或锁存能力；当前仅开放 PCIe-1E16。");
            var result = Call(() => api.GetBoardHandle(handle));
            Run(() => api.SetBoardTimeout(result, Remaining()));
            Remaining();
            return result;
        }
        int OutputCapacity(uint slave, bool packed = false)
        {
            var range = Call(() => api.GetSlavePiRange(handle, PiArea.LocalOutput, slave));
            var bytes = checked((long)range.Length * (packed ? 2 : 1));
            if (bytes > ZlgNativeApi.MaximumBufferBytes) throw new InvalidOperationException("从站输出范围超过插件缓冲上限。");
            Remaining();
            return (int)Math.Max(1, bytes);
        }
        switch (op)
        {
            case "ReadSdo":
                var raw = Call(() => api.ReadSdo(handle, Slave(), checked((ushort)U("Index")), checked((byte)U("SubIndex")), r.Get<int>("Capacity"), Remaining(), r.Get<bool>("CompleteAccess")));
                return ZlgDataCodec.Decode(raw, r.Get<PdoDataType>("DataType"));
            case "WriteSdo":
                var data = r.Get<byte[]>("Data").ToArray(); var slave = Slave(); var index = checked((ushort)U("Index")); var sub = checked((byte)U("SubIndex")); var complete = r.Get<bool>("CompleteAccess");
                Run(() => api.WriteSdo(handle, slave, index, sub, data, Remaining(), complete));
                if (r.Get<bool>("VerifyReadback"))
                {
                    var readback = Call(() => api.ReadSdo(handle, slave, index, sub, data.Length, Remaining(), complete));
                    if (!data.AsSpan().SequenceEqual(readback)) throw new InvalidOperationException($"从站 {slave} 对象 0x{index:X4}:{sub} 写后读回不一致，已提交的写入不会自动回滚。");
                }
                return null;
            case "EnumerateSlaves":
                return DescribeSlaves(api, handle, ct: ct, beforeCall: () => Remaining());
            case "ReadSlaveInfo": return DescribeSlaveInfo(api, handle, Slave(), ct, () => Remaining());
            case "ReadSlaveStatus": return DescribeSlaveStatus(api, handle, Slave(), ct, () => Remaining());
            case "ReadDiagnostics":
                var slaveCount = Call(() => api.GetSlaveCount(handle));
                var diagnostics = Call(() => api.ReadDiagnostics(handle, Enumerable.Range(0, slaveCount).Select(i => (ushort)i).ToArray()));
                return new ZlgDiagnosticResult(diagnostics, (diagnostics.State & 1) != 0, (diagnostics.State & 0x200) != 0, (diagnostics.State & 0x400) != 0, (diagnostics.State & 0x800) != 0);
            case "ReadLinkDiagnostics": return new { FirstLinkDown = Call(() => api.ReadFirstLinkDown(handle)), Faults = Call(() => api.ReadLinkFaults(handle, 1024)) };
            case "ReadLastError": return Call(() => api.ReadLastError(handle, 65536));
            case "ClearSlaveError": Run(() => api.ClearSlaveError(handle, Slave())); return null;
            case "ConfigureAutoClear": Run(() => api.SetAutoClearSlaveError(handle, r.Get<bool>("Enabled"))); return Call(() => api.GetAutoClearSlaveError(handle));
            case "ConfigureReadyLost": Run(() => api.SetReadyLostTimeout(handle, U("ReadyLostTimeoutMs"))); return Call(() => api.GetReadyLostTimeout(handle));
            case "ConfigureWatchdog": Run(() => api.ConfigureWatchdog(handle, Slave(), r.Get<byte[]>("Mask").ToArray(), r.Get<byte[]>("Data").ToArray())); return null;
            case "ReadWatchdog": return Call(() => api.ReadWatchdog(handle, Slave(), OutputCapacity(Slave())));
            case "EnableWatchdog": Run(() => api.EnableWatchdog(handle, U("WatchdogTimeoutMs"))); return null;
            case "DisableWatchdog": Run(() => api.EnableWatchdog(handle, 0)); return null;
            case "FeedWatchdog": Run(() => api.FeedWatchdog(handle)); return null;
            case "ConfigureForceOutput":
                var forced = r.Get<byte[]>("Data").ToArray(); var offset = U("Offset");
                var outputRange = Call(() => api.GetSlavePiRange(handle, PiArea.LocalOutput, Slave()));
                if (forced.Length == 0 || (ulong)offset + (uint)forced.Length > outputRange.Length) throw new ArgumentException("强制输出超出从站过程映像范围。");
                Run(() => api.ConfigureForcedOutput(handle, Slave(), 1, offset, forced)); return null;
            case "ReadForceOutput": return Call(() => api.ReadForcedOutput(handle, Slave(), OutputCapacity(Slave(), true)));
            case "DisableForceOutput": Run(() => api.ConfigureForcedOutput(handle, Slave(), 0, 0, [])); return null;
            case "FoeRead": RequireMailboxMaintenance(); Run(() => api.ReadFoe(handle, Slave(), r.Get<string>("LocalPath"), r.Get<string>("RemotePath"))); return null;
            case "FoeWrite": RequireMailboxMaintenance(); Run(() => api.WriteFoe(handle, Slave(), r.Get<string>("LocalPath"), r.Get<string>("RemotePath"))); return null;
            case "ReadSlaveMemory": case "ReadSlaveEeprom":
                RequireMailboxMaintenance(); return Call(() => api.ReadSlaveMemory(handle, Slave(), checked((ushort)U("Address")), checked((ushort)U("Length")), op == "ReadSlaveEeprom"));
            case "WriteSlaveMemory": case "WriteSlaveEeprom":
                RequireMailboxMaintenance(); Run(() => api.WriteSlaveMemory(handle, Slave(), checked((ushort)U("Address")), r.Get<byte[]>("Data").ToArray(), op == "WriteSlaveEeprom")); return null;
            case "ResetChannel": RequireStopped(); Run(() => api.ResetChannel(handle)); return null;
            case "StartCapture": Run(() => api.StartCapture(handle, U("Capacity"))); return null;
            case "ReadCapture": return Call(() => api.ReadCapture(handle, r.Get<int>("Capacity")));
            case "StopCapture": Run(() => api.StopCapture(handle)); return null;
            case "ReadBoardDi": return Call(() => api.ReadBoardDi(BoardHandle()));
            case "ReadBoardDo": return Call(() => api.ReadBoardDo(BoardHandle()));
            case "WriteBoardDo": Run(() => api.WriteBoardDo(BoardHandle(), U("Value"))); return null;
            case "StartBoardPwm": Run(() => api.OutputPwm(BoardHandle(), U("Port"), U("FrequencyHz"), U("Duty"))); return null;
            case "StopBoardPwm": Run(() => api.StopPwm(BoardHandle(), U("Port"))); return null;
            case "ReadBoardPwm": return Call(() => api.ReadPwmConfiguration(BoardHandle(), U("Port")));
            case "ConfigureBoardPulse":
                Run(() => api.ConfigurePulse(BoardHandle(), U("Port"), new PulseConfiguration(U("PulseMode"), r.Get<bool>("Continuous") ? 0u : 1u, U("Direction"), U("FrequencyHz"), U("Duty")))); return null;
            case "OutputBoardPulse": Run(() => api.OutputPulse(BoardHandle(), U("Port"), U("PulseCount"))); return null;
            case "StopBoardPulse": Run(() => api.StopPulse(BoardHandle(), U("Port"))); return null;
            case "ReadBoardPulse": return Call(() => api.ReadPulseConfiguration(BoardHandle(), U("Port")));
            case "ConfigureBoardLatch":
                var configuredLatch = LatchState(); configuredLatch.ClearLatchConfiguration();
                var continuousLatch = r.Get<bool>("Continuous");
                Run(() => api.ConfigureLatch(BoardHandle(), U("PortSelection"), U("LatchMode"), continuousLatch));
                configuredLatch.LatchContinuous = continuousLatch; return null;
            case "StartBoardLatch":
                var startingLatch = LatchState(); var startContinuous = r.Get<bool>("Continuous");
                if (startingLatch.LatchContinuous != startContinuous) throw new InvalidOperationException("锁存启动模式与本连接已应用配置不一致，请先配置锁存。");
                startingLatch.ClearLatchCapture();
                var latch = BoardHandle(); Run(() => api.EnableLatch(latch, true));
                Run(() => api.ClearLatch(latch, false)); // 清除旧触发状态，不复位编码器计数器。
                var captureCount = U("Count"); Run(() => api.StartLatch(latch, startContinuous, captureCount));
                startingLatch.LatchCaptureCount = startContinuous ? captureCount : 1;
                startingLatch.LatchStarted = true; return null;
            case "StopBoardLatch":
                LatchState().ClearLatchCapture();
                var stoppingLatch = BoardHandle(); Run(() => api.StopLatch(stoppingLatch)); Run(() => api.EnableLatch(stoppingLatch, false)); return null;
            case "ReadBoardLatch":
                var readingLatch = LatchState(); var readContinuous = r.Get<bool>("Continuous");
                if (!readingLatch.LatchStarted || readingLatch.LatchContinuous != readContinuous)
                    throw new InvalidOperationException("本连接尚未成功启动相同模式的锁存，不能读取未确认的缓冲槽。");
                var sample = U("SampleIndex");
                if (readingLatch.LatchCaptureCount is not uint configuredCount || sample >= configuredCount)
                    throw new ArgumentOutOfRangeException("SampleIndex", "样本索引超出本连接启动时配置的记录容量。");
                var readingHandle = BoardHandle(); var port = U("Port");
                if (Call(() => api.ReadLatchState(readingHandle, port)) != 1) throw new InvalidOperationException("指定通道尚未触发锁存，不能把缓冲槽旧值当作采样结果。");
                // SDK 只提供锁存触发状态，没有逐槽写入计数；返回指定硬件槽值，不承诺它是本次触发的新样本。
                return Call(() => api.ReadLatch(readingHandle, port, readContinuous, sample));
            case "ResetBoardLatchCounter":
                LatchState().ClearLatchCapture(); Run(() => api.ClearLatch(BoardHandle(), true)); return null;
            case "ResetBoard": RequireStopped(); operationState?.ClearLatchConfiguration(); Run(() => api.ResetBoard(BoardHandle(false))); return null;
            case "BoardFileRead": RequireStopped(); Run(() => api.ReadBoardFile(BoardHandle(false), r.Get<string>("LocalPath"), r.Get<string>("RemotePath"))); return null;
            case "BoardFileWrite": RequireStopped(); Run(() => api.WriteBoardFile(BoardHandle(false), r.Get<string>("LocalPath"), r.Get<string>("RemotePath"))); return null;
            case "BoardUpdate": RequireStopped(); Run(() => api.UpdateBoard(BoardHandle(false), r.Get<string>("LocalPath"))); return null;
            default: throw new ArgumentException($"SDK 操作缺少处理分支：{op}", nameof(op));
        }
    }
}
