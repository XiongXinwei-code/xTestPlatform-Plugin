using LabVIEWCallPlugin.LVadapter.Interop;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LabVIEWCallPlugin.LVadapter
{
    /// <summary>
    /// VI 状态枚举（对应头文件中的 Enum）
    /// </summary>
    public enum VIState : uint
    {
        Bad = 0,
        Idle = 1,
        RunTopLevel = 2,
        Running = 3
    }

    /// <summary>
    /// LabVIEW Uint32 数组结构（对应 LabVIEW Handle，即 Uint32ArrayBase**）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Uint32ArrayBase
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] dimSizes;   // [height, width]
        public uint firstElement;
    }

    /// <summary>
    /// lvLib.dll 统一 P/Invoke 封装类。
    /// 对应最新头文件 lvLib.h（2026-04-17 更新）：
    ///   - ConnectLabVIEW 简化为只接收 Port，返回 int32_t 状态码（非 0 表示失败）
    ///   - 其余函数签名保持不变
    /// </summary>
    public static class LvLibWrapper
    {
        private const string DllName = "lvLib.dll";

        static LvLibWrapper() => NativeLibrary.SetDllImportResolver(typeof(LvLibWrapper).Assembly, LvBridgeLibrary.Resolve);

        // ── LVfunction_* 系列 ──────────────────────────────────────────────────

        /// <summary>
        /// 获取连接面板信息（对应头文件 LVfunction_GetConnectPanel）。
        /// 注意：LVBoolean* 映射为 [MarshalAs(UnmanagedType.U1)] out bool（1 字节）。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern void LVfunction_GetConnectPanel(
            [MarshalAs(UnmanagedType.LPStr)] string viPath,
            [In, Out] byte[] controlIn,
            [In, Out] byte[] indicatorIn,
            ushort port,
            [MarshalAs(UnmanagedType.U1)] out bool isReentrant,
            out VIState viState,
            [Out] byte[] controlOut,
            [Out] byte[] indicatorOut,
            ref IntPtr pixmapHandle,
            int len,
            int len2);

        /// <summary>编辑 VI 文件（对应头文件 LVfunction_EditVI）。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern int LVfunction_EditVI(
            [MarshalAs(UnmanagedType.LPStr)] string viPath,
            ushort port);

        /// <summary>
        /// 获取 LabVIEW 库中的项目列表（对应头文件 LVfunction_GetLibItems）。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern void LVfunction_GetLibItems(
            [MarshalAs(UnmanagedType.LPStr)] string lvLibPath,
            ushort port,
            [Out] byte[] itemPaths,
            out int error,
            int len);

        // ── LVrte_* 运行时系列 ────────────────────────────────────────────────

        /// <summary>
        /// 加载 VI（对应头文件 LVrte_LoadVI）。
        /// </summary>
        [DllImport(DllName, EntryPoint = "LVrte_LoadVI", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern void NativeLoadVI(
            ushort port,
            [MarshalAs(UnmanagedType.LPStr)] string viPath,
            out uint viReference,
            out int error);

        public static void LVrte_LoadVI(ushort port, string viPath, out IntPtr viReference, out int error)
        {
            NativeLoadVI(port, viPath, out uint reference, out error);
            viReference = FromNativeRefNum(reference);
        }

        /// <summary>
        /// 运行 VI（对应头文件 LVrte_RunVI）。
        /// </summary>
        [DllImport(DllName, EntryPoint = "LVrte_RunVI", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeRunVI(
            ref uint viReference,
            [MarshalAs(UnmanagedType.U1)] bool openPanel,
            [MarshalAs(UnmanagedType.U1)] bool closePanel,
            out uint referenceOut,
            out int error);

        public static int LVrte_RunVI(ref IntPtr viReference, bool openPanel, bool closePanel,
            out IntPtr referenceOut, out int error)
        {
            uint source = ToNativeRefNum(viReference);
            int status = NativeRunVI(ref source, openPanel, closePanel, out uint returned, out error);
            viReference = FromNativeRefNum(source);
            referenceOut = FromNativeRefNum(returned);
            return status;
        }

        // extcode.h: LVRefNum = MagicCookie = uInt32, including on Windows x64.
        // Preserve the public managed signature but never truncate a pointer into a cookie.
        internal static uint ToNativeRefNum(IntPtr reference)
        {
            long value = reference.ToInt64();
            if (value <= 0 || value > uint.MaxValue)
                throw new ArgumentException("VI 引用无效，必须是非零的 32 位无符号 LVRefNum。", nameof(reference));
            return (uint)value;
        }

        internal static IntPtr FromNativeRefNum(uint reference) => new((long)reference);

        /// <summary>
        /// 设置 VI 控件参数（对应头文件 LVrte_SetParameters）。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern int LVrte_SetParameters(
            ref uint reference,
            [MarshalAs(UnmanagedType.LPStr)] string parameterName,
            [In] short[] typeString,
            [In] byte[] dataString,
            out uint referenceOut,
            out int error,
            int len,
            int len2);

        /// <summary>
        /// 获取 VI 指示器参数（对应头文件 LVrte_GetParameters）。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern int LVrte_GetParameters(
            ref uint reference,
            [MarshalAs(UnmanagedType.LPStr)] string parameterName,
            out uint referenceOut,
            [In, Out] short[] typeString,
            [In, Out] byte[] dataString,
            out int error,
            int len,
            int len2);

        /// <summary>
        /// 连接 LabVIEW（对应头文件 ConnectLabVIEW）。
        /// 返回值：0 = 成功，非 0 = 失败。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ConnectLabVIEW(ushort port);

        // ── 内存管理 ──────────────────────────────────────────────────────────

        /// <summary>分配 Uint32 数组内存（对应头文件 AllocateUint32Array）。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr AllocateUint32Array([In] int[] dimSizeArr);

        /// <summary>调整 Uint32 数组大小（对应头文件 ResizeUint32Array）。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ResizeUint32Array(ref IntPtr hdlPtr, [In] int[] dimSizeArr);

        /// <summary>释放 Uint32 数组内存（对应头文件 DeAllocateUint32Array）。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int DeAllocateUint32Array(ref IntPtr hdlPtr);

        // ── 辅助函数 ──────────────────────────────────────────────────────────

        /// <summary>获取 DLL 状态（对应头文件 LVDLLStatus）。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern int LVDLLStatus(
            StringBuilder errStr,
            int errStrLen,
            IntPtr module);

        /// <summary>
        /// 设置是否在私有执行系统中执行 VI（对应头文件 SetExecuteVIsInPrivateExecutionSystem）。
        /// Bool32 → UnmanagedType.Bool（4 字节）。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetExecuteVIsInPrivateExecutionSystem(
            [MarshalAs(UnmanagedType.Bool)] bool value);

        // ── 高层安全封装 ──────────────────────────────────────────────────────

        /// <summary>获取 DLL 状态字符串。</summary>
        public static string GetDllStatus(IntPtr module = default)
        {
            const int bufferSize = 1024;
            var sb = new StringBuilder(bufferSize);
            int code = LVDLLStatus(sb, bufferSize, module);
            return code != 0 ? $"错误码 {code}: {sb}" : sb.ToString();
        }

        /// <summary>
        /// 安全连接 LabVIEW，失败时返回 false 并附带错误消息。
        /// </summary>
        public static bool SafeConnect(ushort port, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                int result = ConnectLabVIEW(port);
                if (result != 0)
                {
                    errorMessage = $"连接失败 - 返回值: {result}";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"连接 LabVIEW 异常: {ex}";
                return false;
            }
        }

        /// <summary>安全加载 VI，失败时返回 false 并附带错误消息。</summary>
        public static bool SafeLoadVI(ushort port, string viPath,
            out IntPtr viReference, out string errorMessage)
            => SafeLoadVI(port, viPath, out viReference, out errorMessage, out _);

        public static bool SafeLoadVI(ushort port, string viPath,
            out IntPtr viReference, out string errorMessage, out Exception? exception)
        {
            viReference = IntPtr.Zero;
            errorMessage = string.Empty;
            exception = null;
            try
            {
                LvAnsiText.Check(viPath, nameof(viPath));
                LVrte_LoadVI(port, viPath, out viReference, out int error);
                if (error != 0)
                {
                    errorMessage = $"加载失败 - 错误码: {error}";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"加载 VI 异常: {ex.Message}";
                exception = ex;
                return false;
            }
        }

        /// <summary>安全运行 VI，失败时返回 false 并附带错误消息。</summary>
        public static bool SafeRunVI(IntPtr viReference, bool openPanel, bool closePanel,
            out IntPtr referenceOut, out string errorMessage)
            => SafeRunVI(viReference, openPanel, closePanel, out referenceOut, out errorMessage, out _);

        public static bool SafeRunVI(IntPtr viReference, bool openPanel, bool closePanel,
            out IntPtr referenceOut, out string errorMessage, out Exception? exception)
        {
            referenceOut = IntPtr.Zero;
            errorMessage = string.Empty;
            exception = null;
            try
            {
                IntPtr viRef = viReference;
                int result = LVrte_RunVI(ref viRef, openPanel, closePanel, out referenceOut, out int error);
                if (result != 0 || error != 0)
                {
                    errorMessage = $"运行失败 - 返回值: {result}, 错误码: {error}";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"运行 VI 异常: {ex.Message}";
                exception = ex;
                return false;
            }
        }

        /// <summary>
        /// 安全设置 VI 控件参数（Flatten 格式）。
        /// </summary>
        public static bool SafeSetParameter(IntPtr viReference, string parameterName,
            short[] typeString, byte[] dataString,
            out IntPtr referenceOut, out string errorMessage)
            => SafeSetParameter(viReference, parameterName, typeString, dataString,
                out referenceOut, out errorMessage, out _);

        public static bool SafeSetParameter(IntPtr viReference, string parameterName,
            short[] typeString, byte[] dataString,
            out IntPtr referenceOut, out string errorMessage, out Exception? exception)
        {
            referenceOut = IntPtr.Zero;
            errorMessage = string.Empty;
            exception = null;
            try
            {
                LvAnsiText.Check(parameterName, nameof(parameterName));
                uint viRef = ToNativeRefNum(viReference);
                int result = LVrte_SetParameters(
                    ref viRef, parameterName,
                    typeString, dataString,
                    out uint returned, out int error,
                    typeString.Length,
                    dataString.Length);
                if (result != 0 || error != 0)
                {
                    errorMessage = $"设置参数失败 - 返回值: {result}, 错误码: {error}";
                    return false;
                }
                referenceOut = FromNativeRefNum(returned);
                LvOutputReader.CheckTerminalReference(viReference, referenceOut);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"设置参数异常: {ex.Message}";
                exception = ex;
                return false;
            }
        }

        /// <summary>
        /// 安全获取 VI 指示器参数（Flatten 格式）。
        /// </summary>
        public static bool SafeGetParameter(IntPtr viReference, string parameterName,
            out IntPtr referenceOut, out short[] typeString, out byte[] dataString,
            out string errorMessage,
            int typeStrShortCount = 128, int dataBufLen = 65536)
            => SafeGetParameter(viReference, parameterName, out referenceOut, out typeString,
                out dataString, out errorMessage, out _, typeStrShortCount, dataBufLen);

        public static bool SafeGetParameter(IntPtr viReference, string parameterName,
            out IntPtr referenceOut, out short[] typeString, out byte[] dataString,
            out string errorMessage, out Exception? exception,
            int typeStrShortCount = 128, int dataBufLen = 65536, CancellationToken ct = default,
            Action? checkAdmission = null)
        {
            referenceOut = IntPtr.Zero;
            typeString = [];
            dataString = [];
            errorMessage = string.Empty;
            exception = null;
            try
            {
                LvAnsiText.Check(parameterName, nameof(parameterName));
                uint cookie = ToNativeRefNum(viReference);
                IntPtr Read(short[] types, byte[] data)
                {
                    uint source = cookie;
                    // lvLib.h: len 为 int16_t 元素数；len2 为 uint8_t 字节数。
                    int status = LVrte_GetParameters(ref source, parameterName, out var returned,
                        types, data, out int error, types.Length, data.Length);
                    if (status != 0 || error != 0)
                        throw new InvalidOperationException($"获取参数失败，返回值 {status}，错误码 {error}");
                    return FromNativeRefNum(returned);
                }
                (referenceOut, typeString, dataString) = LvOutputReader.Read(Read, typeStrShortCount, dataBufLen,
                    ct, viReference, checkAdmission);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                errorMessage = $"获取参数异常: {ex.Message}";
                exception = ex;
                return false;
            }
        }
    }
}
