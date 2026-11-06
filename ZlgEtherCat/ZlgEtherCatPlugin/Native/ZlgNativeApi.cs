using System.Runtime.InteropServices;

namespace ZlgEtherCat.Native;

/// <summary>厂商 C API 的同步封装；构造不加载 DLL。调用方负责共享句柄、调用退出及设备生命周期。</summary>
public partial class ZlgNativeApi : IZlgNativeApi
{
    public const int MaximumBufferBytes = 16 * 1024 * 1024;

    public virtual DriverDetails GetDriverInfo()
    { Check(NativeMethods.DriverGetInfo(out var x), "DriverGetInfo"); return new(Text(x.DriverVersion), Text(x.ApiVersion), x.BoardCount); }
    public virtual BoardDetails EnumerateBoard(uint index)
    { Check(NativeMethods.DriverEnumBoard(index, out var x), "DriverEnumBoard"); return Board(x); }
    public virtual uint GetQueueCapacity(uint boardIndex, uint channel)
    { Check(NativeMethods.DriverEnumEcat(boardIndex, channel, out var x), "DriverEnumEcat"); return x.QueueDepth; }
    public virtual IntPtr Open(string boardInterface, uint channel)
    {
        ValidateString(boardInterface);
        Check(NativeMethods.EcatOpen(out var handle, boardInterface, channel), "EcatOpen");
        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) throw new InvalidOperationException("ZLG 打开接口返回无效句柄。");
        return handle;
    }
    public virtual void Close(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatClose(handle), "EcatClose"); }
    public virtual void SetTimeout(IntPtr handle, uint timeoutMs) { Handle(handle); Timeout(timeoutMs); Check(NativeMethods.EcatSetTimeout(handle, timeoutMs), "EcatSetTimeout"); }
    public virtual void SetBoardTimeout(IntPtr boardHandle, uint timeoutMs) { Handle(boardHandle); Timeout(timeoutMs); Check(NativeMethods.BoardSetTimeout(boardHandle, timeoutMs), "BoardSetTimeout"); }
    public virtual BoardDetails GetBoardInfo(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatGetInfo(handle, out var x), "EcatGetInfo"); return Board(x); }
    public virtual IntPtr GetBoardHandle(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatGetBoardHandle(handle, out var x), "EcatGetBoardHandle"); Handle(x); return x; }
    public virtual void StartBus(IntPtr handle, string eniPath) { Handle(handle); ValidateString(eniPath); Check(NativeMethods.EcatBusRun(handle, eniPath), "EcatBusRun"); }
    public virtual void StopBus(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatBusStop(handle), "EcatBusStop"); }
    public virtual void ResetChannel(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatReset(handle), "EcatReset"); }
    public virtual uint GetBusStatus(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatBusStatus(handle, out var x), "EcatBusStatus"); return x; }
    public virtual bool IsBusRunning(IntPtr handle) { Handle(handle); return NativeMethods.EcatBusIsRunning(handle); }
    public virtual EniDetails GetEniInfo(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatGetEniInfo(handle, out var x), "EcatGetEniInfo"); return new(x.MainCycle, x.MailboxCycle, x.OutputBytes, x.InputBytes); }
    public virtual void CheckRunningEni(IntPtr handle, string eniPath) { Handle(handle); ValidateString(eniPath); Check(NativeMethods.EcatCheckRunningEni(handle, eniPath), "EcatCheckRunningEni"); }
    public virtual void SetPiReinitMode(IntPtr handle, PiReinitMode mode) { Handle(handle); Defined(mode); Check(NativeMethods.EcatPIReinitModeSet(handle, (uint)mode), "EcatPIReinitModeSet"); }
    public virtual PiReinitMode GetPiReinitMode(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatPIReinitModeGet(handle, out var x), "EcatPIReinitModeGet"); Defined((PiReinitMode)x); return (PiReinitMode)x; }
    public virtual void RequestMasterState(IntPtr handle, byte state)
    { Handle(handle); if (state is not (1 or 2 or 3 or 4 or 8)) throw new ArgumentOutOfRangeException(nameof(state)); Check(NativeMethods.EcatRequestMasterState(handle, state), "EcatRequestMasterState"); }
    public virtual byte GetMasterState(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatGetMasterState(handle, out var x), "EcatGetMasterState"); return x; }
    public virtual ushort GetSlaveCount(IntPtr handle, bool activeOnly = false)
    { Handle(handle); ushort x; var code = activeOnly ? NativeMethods.EcatGetActiveSlaveCount(handle, out x) : NativeMethods.EcatGetSlaveCount(handle, out x); Check(code, activeOnly ? "EcatGetActiveSlaveCount" : "EcatGetSlaveCount"); return x; }
    public virtual bool IsSlaveActive(IntPtr handle, ushort slave)
    { Handle(handle); return NativeMethods.EcatSlaveIsActive(handle, slave); }
    public virtual bool IsSlaveReady(IntPtr handle, ushort slave)
    { Handle(handle); return NativeMethods.EcatSlaveIsReady(handle, slave); }
    public virtual ushort GetSlaveFixedAddress(IntPtr handle, ushort slave)
    { Handle(handle); Check(NativeMethods.EcatGetSlaveFixAddr(handle, slave, out var address), "EcatGetSlaveFixAddr"); return address; }
    public virtual SlaveDetails GetSlaveInfo(IntPtr handle, ushort slave)
    { Handle(handle); Check(NativeMethods.EcatGetSlaveInfo(handle, slave, out var x), "EcatGetSlaveInfo"); return new(x.VendorId, x.ProductCode, x.SerialNumber, x.Revision, x.Alias, x.InputBits, x.OutputBits); }
    public virtual byte GetSlaveState(IntPtr handle, ushort slave) { Handle(handle); Check(NativeMethods.EcatGetSlaveState(handle, slave, out var x), "EcatGetSlaveState"); return x; }
    public virtual ushort GetSlaveAlStatus(IntPtr handle, ushort slave) { Handle(handle); Check(NativeMethods.EcatGetSlaveALStatusCode(handle, slave, out var x), "EcatGetSlaveALStatusCode"); return x; }
    public virtual ushort GetSlaveDlStatus(IntPtr handle, ushort slave) { Handle(handle); Check(NativeMethods.EcatGetSlaveDLStatus(handle, slave, out var x), "EcatGetSlaveDLStatus"); return x; }
    public virtual PiRange GetSlavePiRange(IntPtr handle, PiArea area, uint slave)
    { Handle(handle); Defined(area); Check(NativeMethods.EcatSlavePIGetRange(handle, (uint)area, slave, out var offset, out var length), "EcatSlavePIGetRange"); return new(offset, length); }
    public virtual void ClearSlaveError(IntPtr handle, ushort slave) { Handle(handle); Check(NativeMethods.EcatClearSlaveStateError(handle, slave), "EcatClearSlaveStateError"); }
    public virtual void SetAutoClearSlaveError(IntPtr handle, bool enabled) { Handle(handle); Check(NativeMethods.EcatAutoClearSlaveStateErrorEnableSet(handle, enabled ? 1u : 0u), "EcatAutoClearSlaveStateErrorEnableSet"); }
    public virtual bool GetAutoClearSlaveError(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatAutoClearSlaveStateErrorEnableGet(handle, out var x), "EcatAutoClearSlaveStateErrorEnableGet"); return x != 0; }
    public virtual void SetReadyLostTimeout(IntPtr handle, uint timeoutMs) { Handle(handle); Check(NativeMethods.EcatReadyLostTimeoutSet(handle, timeoutMs), "EcatReadyLostTimeoutSet"); }
    public virtual uint GetReadyLostTimeout(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatReadyLostTimeoutGet(handle, out var x), "EcatReadyLostTimeoutGet"); return x; }
    public virtual void EnablePi(IntPtr handle, bool enabled) { Handle(handle); Check(NativeMethods.EcatPIEnableSet(handle, enabled ? 1u : 0u), "EcatPIEnableSet"); }
    public virtual bool IsPiEnabled(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatPIEnableGet(handle, out var x), "EcatPIEnableGet"); return x != 0; }
    public virtual void SetPiAccessMode(IntPtr handle, PiAccessMode mode) { Handle(handle); Defined(mode); Check(NativeMethods.EcatPIASyncModeSet(handle, (uint)mode), "EcatPIASyncModeSet"); }
    public virtual PiAccessMode GetPiAccessMode(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatPIASyncModeGet(handle, out var x), "EcatPIASyncModeGet"); Defined((PiAccessMode)x); return (PiAccessMode)x; }
    public virtual void UpdateAsyncPi(IntPtr handle, PiArea area)
    { Handle(handle); if (area is not (PiArea.FastOutput or PiArea.ShadowInput)) throw new ArgumentException("只有异步输出和输入影子区允许手动同步。", nameof(area)); Check(NativeMethods.EcatPIASyncUpdateData(handle, (uint)area), "EcatPIASyncUpdateData"); }
    public virtual byte[] ReadPi(IntPtr handle, PiArea area, uint offset, int length)
    { Handle(handle); Defined(area); Bounds(offset, length); var data = new byte[length]; Check(NativeMethods.EcatPIRead(handle, (uint)area, offset, (uint)length, data), "EcatPIRead"); return data; }
    public virtual void WritePi(IntPtr handle, PiArea area, uint offset, byte[] data)
    { Handle(handle); WriteArea(area); Data(data); Bounds(offset, data.Length); Check(NativeMethods.EcatPIWrite(handle, (uint)area, offset, (uint)data.Length, data), "EcatPIWrite"); }
    public virtual void MaskWritePi(IntPtr handle, PiArea area, uint offset, byte[] mask, byte[] data)
    { Handle(handle); WriteArea(area); Masks(mask, data); Bounds(offset, data.Length); Check(NativeMethods.EcatPIMaskWrite(handle, (uint)area, offset, (uint)data.Length, mask, data), "EcatPIMaskWrite"); }
    public virtual void ClearPi(IntPtr handle, PiArea area)
    { Handle(handle); if (area is not (PiArea.LocalInput or PiArea.LocalOutput or PiArea.FastOutput)) throw new ArgumentException("指定映像区不允许清零。", nameof(area)); Check(NativeMethods.EcatPIClear(handle, (uint)area), "EcatPIClear"); }
    public virtual bool PopInput(IntPtr handle, bool latest, uint timeoutMs)
    { Handle(handle); Timeout(timeoutMs); return QueueResult(NativeMethods.EcatPIInputQueuePop(handle, latest, timeoutMs), "EcatPIInputQueuePop"); }
    public virtual void PushOutput(IntPtr handle, bool clearOldQueue, uint timeoutMs)
    { Handle(handle); Timeout(timeoutMs); Check(NativeMethods.EcatPIOutputQueuePush(handle, clearOldQueue, timeoutMs), "EcatPIOutputQueuePush"); }
    public virtual bool WaitInputNotification(IntPtr handle, uint timeoutMs)
    { Handle(handle); Timeout(timeoutMs); return QueueResult(NativeMethods.EcatPIInputWaitNotify(handle, timeoutMs), "EcatPIInputWaitNotify"); }
    public virtual uint GetInputQueueSize(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatPIInputQueueSize(handle, out var x), "EcatPIInputQueueSize"); return x; }
    public virtual uint GetOutputQueueSize(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatPIOutputQueueSize(handle, out var x), "EcatPIOutputQueueSize"); return x; }
    public virtual byte[] ReadSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, int capacity, uint timeoutMs, bool completeAccess = false)
    {
        Handle(handle); Length(capacity); Timeout(timeoutMs); CompleteAccess(subIndex, completeAccess);
        var data = new byte[capacity]; uint count = (uint)capacity;
        Check(NativeMethods.EcatCoeSDOUploadEx(handle, slave, index, subIndex, completeAccess ? (byte)1 : (byte)0, ref count, data, timeoutMs), "EcatCoeSDOUploadEx");
        return Actual(data, count);
    }
    public virtual void WriteSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, byte[] data, uint timeoutMs, bool completeAccess = false)
    { Handle(handle); Data(data); Timeout(timeoutMs); CompleteAccess(subIndex, completeAccess); Check(NativeMethods.EcatCoeSDODownloadEx(handle, slave, index, subIndex, completeAccess ? (byte)1 : (byte)0, (uint)data.Length, data, timeoutMs), "EcatCoeSDODownloadEx"); }
    public virtual void ConfigureWatchdog(IntPtr handle, uint slave, byte[] mask, byte[] data)
    { Handle(handle); Masks(mask, data); Check(NativeMethods.EcatPIWatchDogConfig(handle, slave, 0, mask, data, (uint)data.Length), "EcatPIWatchDogConfig"); }
    public virtual WatchdogDetails ReadWatchdog(IntPtr handle, uint slave, int capacity)
    { Handle(handle); Length(capacity); var mask = new byte[capacity]; var data = new byte[capacity]; uint count = (uint)capacity; Check(NativeMethods.EcatPIWatchDogQuery(handle, slave, out var type, out var timeout, mask, data, ref count), "EcatPIWatchDogQuery"); return new(type, timeout, Actual(mask, count), Actual(data, count)); }
    public virtual void EnableWatchdog(IntPtr handle, uint timeoutMs) { Handle(handle); Check(NativeMethods.EcatPIWatchDogEnable(handle, timeoutMs), "EcatPIWatchDogEnable"); }
    public virtual void FeedWatchdog(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatPIWatchDogFeed(handle), "EcatPIWatchDogFeed"); }
    public virtual void ConfigureForcedOutput(IntPtr handle, uint slave, uint commandType, uint offset, byte[] data)
    {
        Handle(handle); ArgumentNullException.ThrowIfNull(data);
        if (commandType > 2) throw new ArgumentOutOfRangeException(nameof(commandType));
        if (commandType != 0) { Data(data); if (commandType == 2 && data.Length % 2 != 0) throw new ArgumentException("强制掩码输出需要完整的掩码与数据对。", nameof(data)); Bounds(offset, commandType == 2 ? data.Length / 2 : data.Length); }
        else if (data.Length != 0) throw new ArgumentException("解除强制输出不得携带输出数据。", nameof(data));
        Check(NativeMethods.EcatSlavePIForceOutputConfig(handle, slave, commandType, offset, (uint)data.Length, data), "EcatSlavePIForceOutputConfig");
    }
    public virtual ForcedOutputDetails ReadForcedOutput(IntPtr handle, uint slave, int capacity)
    { Handle(handle); Length(capacity); var data = new byte[capacity]; uint count = (uint)capacity; Check(NativeMethods.EcatSlavePIForceOutputQuery(handle, slave, out var type, out var offset, ref count, data), "EcatSlavePIForceOutputQuery"); return new(type, offset, Actual(data, count)); }

    private static BoardDetails Board(NativeStructures.BoardInfo x) => new(Text(x.Name), Text(x.Version), Text(x.SerialNumber), x.Alias, x.ChannelCount, x.FpgaVersion);
    internal static void Handle(IntPtr handle) { if (handle == IntPtr.Zero || handle == new IntPtr(-1)) throw new ArgumentException("设备句柄无效。", nameof(handle)); }
    internal static void Length(int length) { if (length <= 0 || length > MaximumBufferBytes) throw new ArgumentOutOfRangeException(nameof(length), "数据长度必须为 1～16777216 字节。"); }
    internal static void Bounds(uint offset, int length) { Length(length); if ((ulong)offset + (uint)length > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(offset), "偏移与长度超出 SDK 地址范围。"); }
    internal static void Data(byte[] data) { ArgumentNullException.ThrowIfNull(data); Length(data.Length); }
    internal static void Masks(byte[] mask, byte[] data) { Data(mask); Data(data); if (mask.Length != data.Length) throw new ArgumentException("掩码与数据长度必须一致。"); }
    internal static void Timeout(uint timeoutMs) { if (timeoutMs == 0 || timeoutMs > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "等待超时必须在 1～2147483647 毫秒之间。"); }
    internal static void Defined<T>(T value) where T : struct, Enum { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), "SDK 枚举值无效。"); }
    internal static void WriteArea(PiArea area) { if (area is not (PiArea.LocalOutput or PiArea.FastOutput)) throw new ArgumentException("仅允许写入本地输出或快速输出映像。", nameof(area)); }
    private static void CompleteAccess(byte subIndex, bool enabled) { if (enabled && subIndex > 1) throw new ArgumentOutOfRangeException(nameof(subIndex), "完全访问仅允许子索引 0 或 1。"); }
    internal static byte[] Actual(byte[] data, uint count)
    { if (count > data.Length) throw new InvalidOperationException("SDK 返回的数据长度超出已分配缓冲区。"); return count == data.Length ? data : data.AsSpan(0, checked((int)count)).ToArray(); }
    internal static void ValidateString(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Contains('\0')) throw new ArgumentException("设备标识或路径不得包含空字符。", nameof(value));
        // SDK 的 char* 编码未声明为 UTF-8，Windows 绑定明确采用当前系统 ANSI 代码页；禁止静默替换字符。
        var ptr = Marshal.StringToHGlobalAnsi(value);
        try { if (!string.Equals(Marshal.PtrToStringAnsi(ptr), value, StringComparison.Ordinal)) throw new ArgumentException("此字符串不能由当前 Windows ANSI 代码页无损表示，请使用可表示的设备标识或路径。", nameof(value)); }
        finally { Marshal.FreeHGlobal(ptr); }
    }
    internal static string Text(byte[] data)
    {
        var count = Array.IndexOf(data, (byte)0); if (count < 0) count = data.Length;
        var ptr = Marshal.AllocHGlobal(count + 1);
        try { Marshal.Copy(data, 0, ptr, count); Marshal.WriteByte(ptr, count, 0); return Marshal.PtrToStringAnsi(ptr) ?? string.Empty; }
        finally { Marshal.FreeHGlobal(ptr); }
    }
    internal static bool QueueResult(int code, string function)
    {
        // 仅解释头文件声明的“无数据/等待超时”；驱动接收超时 80003 等仍作为通信故障保留。
        if (code is 2 or 5 or 32881) return false;
        Check(code, function); return true;
    }
    internal static void Check(int code, string function)
    {
        if (code == 0) return;
        var pointer = NativeMethods.DriverGetErrStr(unchecked((uint)code));
        var detail = pointer == IntPtr.Zero ? "厂商未提供错误说明" : Marshal.PtrToStringAnsi(pointer) ?? "厂商未提供错误说明";
        throw new ZlgNativeException(function, code, detail);
    }
}
