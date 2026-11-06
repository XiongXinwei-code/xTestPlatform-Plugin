using System;
using System.Runtime.InteropServices;

namespace ZlgEtherCat.Native;

// 只声明已随包验证导出的 C API；生产调用仅由 ZlgNativeApi 发起。
internal static class NativeMethods
{
    private const string Library = "pci_zecm.dll";

    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, (name, assembly, _) =>
        {
            if (name != Library) return IntPtr.Zero;
            if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("ZLG EtherCAT 插件仅支持 Windows x64 进程。");
            var directory = System.IO.Path.GetDirectoryName(assembly.Location)
                ?? throw new InvalidOperationException("无法定位 ZLG 插件程序集目录。");
            return NativeLibrary.Load(System.IO.Path.Combine(directory, "Native", Library), assembly,
                DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.System32);
        });
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int DriverGetInfo(out NativeStructures.DriverInfo info);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int DriverEnumBoard(uint index, out NativeStructures.BoardInfo info);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int DriverEnumEcat(uint index, uint channel, out NativeStructures.EcatInfo info);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr DriverGetErrStr(uint error);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatOpen(out IntPtr handle, string board, uint channel);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatClose(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatSetTimeout(IntPtr handle, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    internal static extern int BoardSetTimeout(IntPtr handle, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetInfo(IntPtr handle, out NativeStructures.BoardInfo info);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetBoardHandle(IntPtr handle, out IntPtr boardHandle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatBusRun(IntPtr handle, string path);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatBusStop(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatReset(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatBusStatus(IntPtr handle, out uint status);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool EcatBusIsRunning(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetEniInfo(IntPtr handle, out NativeStructures.EniInfo info);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatCheckRunningEni(IntPtr handle, string path);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIReinitModeSet(IntPtr handle, uint mode);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIReinitModeGet(IntPtr handle, out uint mode);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatRequestMasterState(IntPtr handle, byte state);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetMasterState(IntPtr handle, out byte state);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetSlaveCount(IntPtr handle, out ushort count);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetActiveSlaveCount(IntPtr handle, out ushort count);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool EcatSlaveIsActive(IntPtr handle, ushort slave);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool EcatSlaveIsReady(IntPtr handle, ushort slave);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int EcatGetSlaveFixAddr(IntPtr handle, ushort slave, out ushort address);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetSlaveInfo(IntPtr handle, ushort slave, out NativeStructures.SlaveInfo info);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetSlaveState(IntPtr handle, ushort slave, out byte state);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetSlaveALStatusCode(IntPtr handle, ushort slave, out ushort status);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetSlaveDLStatus(IntPtr handle, ushort slave, out ushort status);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatSlavePIGetRange(IntPtr handle, uint area, uint slave, out uint offset, out uint length);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatClearSlaveStateError(IntPtr handle, ushort slave);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatAutoClearSlaveStateErrorEnableSet(IntPtr handle, uint enabled);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatAutoClearSlaveStateErrorEnableGet(IntPtr handle, out uint enabled);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatReadyLostTimeoutSet(IntPtr handle, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatReadyLostTimeoutGet(IntPtr handle, out uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIEnableSet(IntPtr handle, uint enabled);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIEnableGet(IntPtr handle, out uint enabled);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIASyncModeSet(IntPtr handle, uint mode);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIASyncModeGet(IntPtr handle, out uint mode);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIASyncUpdateData(IntPtr handle, uint area);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIRead(IntPtr handle, uint area, uint offset, uint length, [Out] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIWrite(IntPtr handle, uint area, uint offset, uint length, [In] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIMaskWrite(IntPtr handle, uint area, uint offset, uint length, [In] byte[] mask, [In] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIClear(IntPtr handle, uint area);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIInputQueuePop(IntPtr handle, [MarshalAs(UnmanagedType.I1)] bool latest, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIOutputQueuePush(IntPtr handle, [MarshalAs(UnmanagedType.I1)] bool clear, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIInputWaitNotify(IntPtr handle, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIInputQueueSize(IntPtr handle, out uint size);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIOutputQueueSize(IntPtr handle, out uint size);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatCoeSDOUploadEx(IntPtr handle, ushort slave, ushort index, byte subIndex, byte completeAccess, ref uint length, [Out] byte[] data, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatCoeSDODownloadEx(IntPtr handle, ushort slave, ushort index, byte subIndex, byte completeAccess, uint length, [In] byte[] data, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIWatchDogConfig(IntPtr handle, uint slave, uint type, [In] byte[] mask, [In] byte[] data, uint size);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIWatchDogQuery(IntPtr handle, uint slave, out uint type, out uint timeoutMs, [Out] byte[] mask, [Out] byte[] data, ref uint size);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIWatchDogEnable(IntPtr handle, uint timeoutMs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatPIWatchDogFeed(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatSlavePIForceOutputConfig(IntPtr handle, uint slave, uint command, uint offset, uint size, [In] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatSlavePIForceOutputQuery(IntPtr handle, uint slave, out uint command, out uint offset, ref uint size, [Out] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetDiagnostics(IntPtr handle, out NativeStructures.MasterDiagnosticInfo master, ushort count, [In] ushort[] slaves, IntPtr diagnostics);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatNetDiagnosticsGetSlaveLinkStatus(IntPtr handle, IntPtr topology, ref ushort count);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetLastError(IntPtr handle, [Out] byte[] data, uint capacity, out uint actualLength);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatGetFirstLinkDownPortStatus(IntPtr handle, out uint status);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardDIRead(IntPtr handle, out uint value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardDORead(IntPtr handle, out uint value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardDOWrite(IntPtr handle, uint value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardPulseConfig(IntPtr handle, uint channel, uint mode, uint triggerMode, uint direction, uint frequency, uint dutyPercent);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardPulseGetConfig(IntPtr handle, uint channel, out uint mode, out uint triggerMode, out uint direction, out uint frequency, out uint dutyPercent);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardPulseOutput(IntPtr handle, uint channel, uint count);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardPulseStop(IntPtr handle, uint channel);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardPWMOutput(IntPtr handle, uint channel, uint frequency, uint dutyPercent);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardPWMGetConfig(IntPtr handle, uint channel, out uint frequency, out uint dutyPercent);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardPWMStop(IntPtr handle, uint channel);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchEnable(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchDisable(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchPortSel(IntPtr handle, uint map);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchCfg(IntPtr handle, uint mode);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardSeqLatchCfg(IntPtr handle, uint mode);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchRun(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardSeqLatchRun(IntPtr handle, uint count);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchStop(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchClear(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchCounterReset(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchOnRead(IntPtr handle, uint channel, out uint low, out uint high);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardSeqLatchOnRead(IntPtr handle, uint channel, uint index, out uint low, out uint high);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardLatchState(IntPtr handle, uint channel, out uint state);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatFoERead(IntPtr handle, ushort slave, string localPath, string remotePath);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatFoEWrite(IntPtr handle, ushort slave, string localPath, string remotePath);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatSlaveEepromRead(IntPtr handle, ushort slave, ushort address, ushort length, [Out] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatSlaveEepromWrite(IntPtr handle, ushort slave, ushort address, ushort length, [In] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatSlaveMemoryRead(IntPtr handle, ushort slave, ushort address, ushort length, [Out] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatSlaveMemoryWrite(IntPtr handle, ushort slave, ushort address, ushort length, [In] byte[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardReset(IntPtr handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardFileUpload(IntPtr handle, string localPath, string remotePath, IntPtr callback, IntPtr context);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardFileDownload(IntPtr handle, string localPath, string remotePath, IntPtr callback, IntPtr context);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int BoardUpdate(IntPtr handle, string localPath);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatWiresharkStartup(IntPtr handle, uint queueSize);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatWiresharkGetData(IntPtr handle, [Out] byte[] data, ref uint size);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    internal static extern int EcatWiresharkStop(IntPtr handle);

}
