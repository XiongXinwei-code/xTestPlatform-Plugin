using System.Runtime.InteropServices;

namespace ZlgEtherCat.Native;

public partial class ZlgNativeApi
{
    public virtual MasterDiagnostics ReadDiagnostics(IntPtr handle, ushort[] slaves)
    {
        Handle(handle); ArgumentNullException.ThrowIfNull(slaves);
        if (slaves.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(slaves));
        var elementSize = Marshal.SizeOf<NativeStructures.SlaveDiagnosticInfo>();
        var memory = Marshal.AllocHGlobal(Math.Max(1, checked(elementSize * slaves.Length)));
        try
        {
            Check(NativeMethods.EcatGetDiagnostics(handle, out var master, (ushort)slaves.Length, slaves, memory), "EcatGetDiagnostics");
            var result = new SlaveDiagnostics[slaves.Length];
            for (var i = 0; i < result.Length; i++)
            {
                var x = Marshal.PtrToStructure<NativeStructures.SlaveDiagnosticInfo>(memory + elementSize * i);
                result[i] = new(x.State, x.Identity.VendorId, x.Identity.ProductCode, x.Identity.Revision, x.Identity.SerialNumber, x.PortCounters, x.ProcessingUnitErrors, x.PdiErrors);
            }
            return new(master.State, master.NetworkState, master.MainSlaveCount, master.RedundancySlaveCount, result);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    public virtual IReadOnlyList<LinkFault> ReadLinkFaults(IntPtr handle, int capacity)
    {
        Handle(handle); if (capacity is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(capacity));
        var elementSize = Marshal.SizeOf<NativeStructures.Topology>();
        var memory = Marshal.AllocHGlobal(checked(elementSize * capacity));
        try
        {
            var count = (ushort)capacity;
            Check(NativeMethods.EcatNetDiagnosticsGetSlaveLinkStatus(handle, memory, ref count), "EcatNetDiagnosticsGetSlaveLinkStatus");
            if (count > capacity) throw new InvalidOperationException("SDK 返回的错误拓扑数量超出缓冲区。");
            var result = new LinkFault[count];
            for (var i = 0; i < result.Length; i++)
            {
                var x = Marshal.PtrToStructure<NativeStructures.Topology>(memory + elementSize * i);
                if (x.Count > 10) throw new InvalidOperationException("SDK 返回的拓扑层级超出头文件规定的 10 层。");
                result[i] = new(x.Status, x.Nodes.Take((int)x.Count).Select(n => new LinkTopologyNode(Text(n.Name), n.VendorId, n.ProductCode, n.Port, n.PhysicalPosition, n.IncrementAddress, n.FixedAddress, n.Alias, n.RequestingId)).ToArray());
            }
            return result;
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    public virtual string ReadLastError(IntPtr handle, int capacity)
    {
        Handle(handle); Length(capacity); var data = new byte[capacity];
        var code = NativeMethods.EcatGetLastError(handle, data, (uint)capacity, out var length);
        if (code == 2) return string.Empty;
        Check(code, "EcatGetLastError");
        // API 允许分片消费，不能把一个截断片段当作完整错误消息。
        if (length > capacity) throw new InvalidOperationException($"错误消息尚有未读分片，SDK 报告剩余长度 {length}，请增大读取容量。已读分片：{Text(data)}");
        return Text(Actual(data, length));
    }
    public virtual uint ReadFirstLinkDown(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatGetFirstLinkDownPortStatus(handle, out var x), "EcatGetFirstLinkDownPortStatus"); return x; }
    public virtual uint ReadBoardDi(IntPtr boardHandle) { Handle(boardHandle); Check(NativeMethods.BoardDIRead(boardHandle, out var x), "BoardDIRead"); return x; }
    public virtual uint ReadBoardDo(IntPtr boardHandle) { Handle(boardHandle); Check(NativeMethods.BoardDORead(boardHandle, out var x), "BoardDORead"); return x; }
    public virtual void WriteBoardDo(IntPtr boardHandle, uint value)
    { Handle(boardHandle); if (value > 65535) throw new ArgumentOutOfRangeException(nameof(value), "板载 DO 仅允许低 16 位。"); Check(NativeMethods.BoardDOWrite(boardHandle, value), "BoardDOWrite"); }
    public virtual void ConfigurePulse(IntPtr boardHandle, uint channel, PulseConfiguration configuration)
    {
        Handle(boardHandle); BoardChannel(channel); ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Mode > 3 || configuration.TriggerMode > 1 || configuration.Direction > 1) throw new ArgumentOutOfRangeException(nameof(configuration));
        Wave(configuration.Frequency, configuration.DutyPercent);
        Check(NativeMethods.BoardPulseConfig(boardHandle, channel, configuration.Mode, configuration.TriggerMode, configuration.Direction, configuration.Frequency, configuration.DutyPercent), "BoardPulseConfig");
    }
    public virtual PulseConfiguration ReadPulseConfiguration(IntPtr boardHandle, uint channel)
    { Handle(boardHandle); BoardChannel(channel); Check(NativeMethods.BoardPulseGetConfig(boardHandle, channel, out var mode, out var trigger, out var direction, out var frequency, out var duty), "BoardPulseGetConfig"); return new(mode, trigger, direction, frequency, duty); }
    public virtual void OutputPulse(IntPtr boardHandle, uint channel, uint count)
    { Handle(boardHandle); BoardChannel(channel); if (count == 0) throw new ArgumentOutOfRangeException(nameof(count)); Check(NativeMethods.BoardPulseOutput(boardHandle, channel, count), "BoardPulseOutput"); }
    public virtual void StopPulse(IntPtr boardHandle, uint channel) { Handle(boardHandle); BoardChannel(channel); Check(NativeMethods.BoardPulseStop(boardHandle, channel), "BoardPulseStop"); }
    public virtual void OutputPwm(IntPtr boardHandle, uint channel, uint frequency, uint dutyPercent)
    { Handle(boardHandle); BoardChannel(channel); Wave(frequency, dutyPercent); Check(NativeMethods.BoardPWMOutput(boardHandle, channel, frequency, dutyPercent), "BoardPWMOutput"); }
    public virtual PwmConfiguration ReadPwmConfiguration(IntPtr boardHandle, uint channel)
    { Handle(boardHandle); BoardChannel(channel); Check(NativeMethods.BoardPWMGetConfig(boardHandle, channel, out var frequency, out var duty), "BoardPWMGetConfig"); return new(frequency, duty); }
    public virtual void StopPwm(IntPtr boardHandle, uint channel) { Handle(boardHandle); BoardChannel(channel); Check(NativeMethods.BoardPWMStop(boardHandle, channel), "BoardPWMStop"); }
    public virtual void EnableLatch(IntPtr boardHandle, bool enabled) { Handle(boardHandle); Check(enabled ? NativeMethods.BoardLatchEnable(boardHandle) : NativeMethods.BoardLatchDisable(boardHandle), enabled ? "BoardLatchEnable" : "BoardLatchDisable"); }
    public virtual void ConfigureLatch(IntPtr boardHandle, uint portMap, uint mode, bool continuous)
    {
        Handle(boardHandle); if (portMap > 255) throw new ArgumentOutOfRangeException(nameof(portMap));
        uint[] supported = continuous ? [101, 102, 103, 104, 114, 115, 116, 117, 118, 119, 120, 121, 123, 124, 133, 134, 135, 136] : [1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21];
        if (!supported.Contains(mode)) throw new ArgumentOutOfRangeException(nameof(mode), "锁存模式不在厂商枚举中。");
        Check(NativeMethods.BoardLatchPortSel(boardHandle, portMap), "BoardLatchPortSel");
        Check(continuous ? NativeMethods.BoardSeqLatchCfg(boardHandle, mode) : NativeMethods.BoardLatchCfg(boardHandle, mode), continuous ? "BoardSeqLatchCfg" : "BoardLatchCfg");
    }
    public virtual void StartLatch(IntPtr boardHandle, bool continuous, uint count)
    { Handle(boardHandle); if (count > 1023) throw new ArgumentOutOfRangeException(nameof(count)); Check(continuous ? NativeMethods.BoardSeqLatchRun(boardHandle, count) : NativeMethods.BoardLatchRun(boardHandle), continuous ? "BoardSeqLatchRun" : "BoardLatchRun"); }
    public virtual void StopLatch(IntPtr boardHandle) { Handle(boardHandle); Check(NativeMethods.BoardLatchStop(boardHandle), "BoardLatchStop"); }
    public virtual void ClearLatch(IntPtr boardHandle, bool resetCounter)
    { Handle(boardHandle); Check(resetCounter ? NativeMethods.BoardLatchCounterReset(boardHandle) : NativeMethods.BoardLatchClear(boardHandle), resetCounter ? "BoardLatchCounterReset" : "BoardLatchClear"); }
    public virtual ulong ReadLatch(IntPtr boardHandle, uint channel, bool continuous, uint sampleIndex)
    { Handle(boardHandle); BoardChannel(channel); if (sampleIndex > 1022) throw new ArgumentOutOfRangeException(nameof(sampleIndex)); uint low, high; Check(continuous ? NativeMethods.BoardSeqLatchOnRead(boardHandle, channel, sampleIndex, out low, out high) : NativeMethods.BoardLatchOnRead(boardHandle, channel, out low, out high), continuous ? "BoardSeqLatchOnRead" : "BoardLatchOnRead"); return ((ulong)high << 32) | low; }
    public virtual uint ReadLatchState(IntPtr boardHandle, uint channel) { Handle(boardHandle); BoardChannel(channel); Check(NativeMethods.BoardLatchState(boardHandle, channel, out var x), "BoardLatchState"); return x; }
    public virtual void ReadFoe(IntPtr handle, ushort slave, string localPath, string remotePath)
    { Handle(handle); ValidateString(localPath); ValidateString(remotePath); Check(NativeMethods.EcatFoERead(handle, slave, localPath, remotePath), "EcatFoERead"); }
    public virtual void WriteFoe(IntPtr handle, ushort slave, string localPath, string remotePath)
    { Handle(handle); ValidateString(localPath); ValidateString(remotePath); Check(NativeMethods.EcatFoEWrite(handle, slave, localPath, remotePath), "EcatFoEWrite"); }
    public virtual byte[] ReadSlaveMemory(IntPtr handle, ushort slave, ushort address, ushort length, bool eeprom)
    {
        Handle(handle); MemoryBounds(address, length); var data = new byte[length];
        Check(eeprom ? NativeMethods.EcatSlaveEepromRead(handle, slave, address, length, data) : NativeMethods.EcatSlaveMemoryRead(handle, slave, address, length, data), eeprom ? "EcatSlaveEepromRead" : "EcatSlaveMemoryRead"); return data;
    }
    public virtual void WriteSlaveMemory(IntPtr handle, ushort slave, ushort address, byte[] data, bool eeprom)
    { Handle(handle); Data(data); if (data.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(data)); MemoryBounds(address, (ushort)data.Length); Check(eeprom ? NativeMethods.EcatSlaveEepromWrite(handle, slave, address, (ushort)data.Length, data) : NativeMethods.EcatSlaveMemoryWrite(handle, slave, address, (ushort)data.Length, data), eeprom ? "EcatSlaveEepromWrite" : "EcatSlaveMemoryWrite"); }
    public virtual void ResetBoard(IntPtr boardHandle) { Handle(boardHandle); Check(NativeMethods.BoardReset(boardHandle), "BoardReset"); }
    public virtual void ReadBoardFile(IntPtr boardHandle, string localPath, string remotePath)
    { Handle(boardHandle); ValidateString(localPath); ValidateString(remotePath); Check(NativeMethods.BoardFileUpload(boardHandle, localPath, remotePath, IntPtr.Zero, IntPtr.Zero), "BoardFileUpload"); }
    public virtual void WriteBoardFile(IntPtr boardHandle, string localPath, string remotePath)
    { Handle(boardHandle); ValidateString(localPath); ValidateString(remotePath); Check(NativeMethods.BoardFileDownload(boardHandle, localPath, remotePath, IntPtr.Zero, IntPtr.Zero), "BoardFileDownload"); }
    public virtual void UpdateBoard(IntPtr boardHandle, string localPath) { Handle(boardHandle); ValidateString(localPath); Check(NativeMethods.BoardUpdate(boardHandle, localPath), "BoardUpdate"); }
    public virtual void StartCapture(IntPtr handle, uint queueSize)
    { Handle(handle); if (queueSize == 0) throw new ArgumentOutOfRangeException(nameof(queueSize)); Check(NativeMethods.EcatWiresharkStartup(handle, queueSize), "EcatWiresharkStartup"); }
    public virtual byte[] ReadCapture(IntPtr handle, int capacity)
    { Handle(handle); Length(capacity); var data = new byte[capacity]; uint size = (uint)capacity; Check(NativeMethods.EcatWiresharkGetData(handle, data, ref size), "EcatWiresharkGetData"); return Actual(data, size); }
    public virtual void StopCapture(IntPtr handle) { Handle(handle); Check(NativeMethods.EcatWiresharkStop(handle), "EcatWiresharkStop"); }
    private static void BoardChannel(uint channel) { if (channel > 3) throw new ArgumentOutOfRangeException(nameof(channel), "板载模块通道范围为 0～3，具体硬件支持须另行核对。"); }
    private static void Wave(uint frequency, uint duty) { if (frequency == 0 || duty > 100) throw new ArgumentOutOfRangeException(nameof(frequency), "频率必须大于 0，占空比范围为 0～100%。"); }
    private static void MemoryBounds(ushort address, ushort length) { if (length == 0 || (uint)address + length > 65536) throw new ArgumentOutOfRangeException(nameof(length), "地址与长度超出 16 位设备存储空间。"); }
}
