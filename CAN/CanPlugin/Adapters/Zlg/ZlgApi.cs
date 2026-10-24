using System.Runtime.InteropServices;
using CAN.Helpers;

namespace CAN.Adapters.Zlg;

/// <summary>周立功 ZLGCAN P/Invoke 封装（zlgcan.dll）</summary>
internal static class ZlgApi
{
    private const string DllName = "zlgcan.dll";

    // ── 原生库加载 ────────────────────────────────────────────────────────────────
    // 插件由宿主动态加载，DllImport 默认搜索的是宿主进程目录而非插件目录，
    // 因此这里把随插件一起发布的 Native\Zlg 目录显式加入搜索路径。
    // zlgcan.dll 还会从自身所在目录的 kerneldlls 下加载各板卡内核驱动，
    // SetDllDirectory 可保证这些依赖同样能被找到。
    // 注意：宿主若以字节流方式加载插件程序集，Assembly.Location 会为空，
    // 故这里按多个候选位置依次探测，而不是只依赖 Location。
    static ZlgApi()
    {
        var tried = new List<string>();
        try
        {
            foreach (var dir in EnumerateCandidateDirs())
            {
                if (string.IsNullOrEmpty(dir) || tried.Contains(dir))
                    continue;
                tried.Add(dir);

                if (!File.Exists(Path.Combine(dir, DllName)))
                    continue;

                NativeDir = dir;
                SetDllDirectory(dir);
                NativeLibrary.SetDllImportResolver(typeof(ZlgApi).Assembly, ResolveNativeLibrary);
                break;
            }
        }
        catch (Exception ex)
        {
            // 准备加载路径失败时退回系统默认搜索顺序（PATH / 已安装驱动目录）
            NativeDir = null;
            tried.Add($"[异常] {ex.Message}");
        }

        ProbedDirs = tried;
    }

    /// <summary>依次列出可能存放 Native\Zlg 的目录（可能包含空项）</summary>
    private static IEnumerable<string?> EnumerateCandidateDirs()
    {
        // 1) 插件程序集自身所在目录（正常从文件加载时）
        string? asmDir = null;
        try
        {
            var loc = typeof(ZlgApi).Assembly.Location;
            if (!string.IsNullOrEmpty(loc))
                asmDir = Path.GetDirectoryName(loc);
        }
        catch { /* 忽略 */ }
        if (asmDir is not null)
            yield return Path.Combine(asmDir, "Native", "Zlg");

        // 2) 宿主基目录下的插件目录（Assembly.Location 为空时的兜底）
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            yield return Path.Combine(baseDir, "Plugins", "CAN", "Native", "Zlg");
            yield return Path.Combine(baseDir, "Native", "Zlg");

            // 3) 插件目录名可能与宿主约定不同，扫描 Plugins 下一层
            var pluginsRoot = Path.Combine(baseDir, "Plugins");
            string[] subDirs;
            try { subDirs = Directory.Exists(pluginsRoot) ? Directory.GetDirectories(pluginsRoot) : []; }
            catch { subDirs = []; }
            foreach (var sub in subDirs)
                yield return Path.Combine(sub, "Native", "Zlg");
        }
    }

    /// <summary>实际使用的原生库目录，未找到时为 null</summary>
    private static string? NativeDir;

    /// <summary>探测过的目录，用于加载失败时输出诊断信息</summary>
    private static readonly IReadOnlyList<string> ProbedDirs;

    /// <summary>原生库实际加载失败的原因，成功时为 null</summary>
    private static string? LoadFailure;

    /// <summary>zlgcan.dll 自身依赖的 VC++ 2013 运行库</summary>
    private static readonly string[] RequiredCrt = ["msvcr120.dll", "msvcp120.dll"];

    /// <summary>检查 zlgcan.dll 依赖的 VC++ 运行库是否可加载，返回缺失项</summary>
    private static List<string> GetMissingCrt()
    {
        var missing = new List<string>();
        foreach (var crt in RequiredCrt)
        {
            try
            {
                if (NativeLibrary.TryLoad(crt, out var h))
                {
                    NativeLibrary.Free(h);
                    continue;
                }
            }
            catch { /* 视为缺失 */ }
            missing.Add(crt);
        }
        return missing;
    }

    /// <summary>生成原生库加载失败的诊断说明</summary>
    internal static string GetLoadDiagnostics()
    {
        var dirs = ProbedDirs.Count == 0 ? "（无）" : string.Join("；", ProbedDirs);

        // zlgcan.dll 依赖 VC++ 2013 运行库，缺失时表现为"找不到 zlgcan.dll"，需优先排查
        var missingCrt = GetMissingCrt();
        var crtHint = missingCrt.Count > 0
            ? $"【很可能的原因】本机缺少 VC++ 2013 运行库（{string.Join("、", missingCrt)}），" +
              "zlgcan.dll 依赖它才能加载。请在测试机安装 Visual C++ Redistributable for Visual Studio 2013 (x64)，" +
              "或安装周立功官方驱动包（其安装程序会一并部署该运行库）。"
            : "VC++ 2013 运行库检查通过。";

        if (NativeDir is null)
            return $"未在以下任何位置找到 {DllName}：{dirs}。{crtHint}";

        var kernelDir = Path.Combine(NativeDir, "kerneldlls");
        var detail = LoadFailure is null
            ? "未进入插件自带的加载流程（请确认运行的插件为最新版本）"
            : $"加载失败：{LoadFailure}";

        return $"已定位原生库目录：{NativeDir}（kerneldlls 存在={Directory.Exists(kernelDir)}）；{detail}；{crtHint}已探测：{dirs}";
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (NativeDir is null || !string.Equals(libraryName, DllName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        var path = Path.Combine(NativeDir, DllName);
        try
        {
            return NativeLibrary.Load(path);
        }
        catch (Exception ex)
        {
            LoadFailure = $"{ex.GetType().Name}: {ex.Message}";
            return IntPtr.Zero;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

    // ── 返回值 ────────────────────────────────────────────
    public const uint STATUS_OK = 1;

    // ── 常用设备类型码 ────────────────────────────────────
    public const uint ZCAN_USBCAN1 = 3;          // USBCAN-I
    public const uint ZCAN_USBCAN2 = 4;          // USBCAN-II
    public const uint ZCAN_USBCAN_E_U = 20;      // USBCAN-E-U
    public const uint ZCAN_USBCAN_2E_U = 21;     // USBCAN-2E-U
    public const uint ZCAN_USBCANFD_200U = 41;   // USBCANFD-200U
    public const uint ZCAN_USBCANFD_100U = 42;   // USBCANFD-100U
    public const uint ZCAN_USBCANFD_MINI = 43;   // USBCANFD-MINI

    public const uint ZCAN_PCI5121 = 1;
    public const uint ZCAN_PCI9810 = 2;
    public const uint ZCAN_PCI9820 = 5;
    public const uint ZCAN_PCI5110 = 7;
    public const uint ZCAN_PCI9840 = 14;
    public const uint ZCAN_PCI9820I = 16;
    public const uint ZCAN_PCIE_9220 = 18;
    public const uint ZCAN_PCI5010U = 19;
    public const uint ZCAN_PCI5020U = 22;
    public const uint ZCAN_PCIE9221 = 24;
    public const uint ZCAN_PCIE9120 = 27;
    public const uint ZCAN_PCIE9110 = 28;
    public const uint ZCAN_PCIE9140 = 29;
    public const uint ZCAN_PCIE_CANFD_100U = 38;
    public const uint ZCAN_PCIE_CANFD_200U = 39;
    public const uint ZCAN_PCIE_CANFD_400U = 40;
    public const uint ZCAN_PCIE_CANFD_100U_EX = 60;
    public const uint ZCAN_PCIE_CANFD_400U_EX = 61;
    public const uint ZCAN_PCIE_CANFD_200U_MINI = 62;
    public const uint ZCAN_PCIE_CANFD_200U_EX = 63; // SDK 中 200U-M2 与 200U-EX 共用设备码
    public const uint ZCAN_PCIE_CANFD_800U = 82;
    public const uint ZCAN_PCIE_CANFD_1200U = 83;
    public const uint ZCAN_MINI_PCIE_CANFD = 84;

    // ── 帧类型 ────────────────────────────────────────────
    public const uint ZCAN_TYPE_CAN = 0;
    public const uint ZCAN_TYPE_CANFD = 1;

    // ── 初始化配置（原生为 union，必须按 CAN/CANFD 两套布局分别映射） ─────
    [StructLayout(LayoutKind.Sequential)]
    public struct ZCAN_CHANNEL_CAN_CFG
    {
        public uint acc_code;
        public uint acc_mask;
        public uint reserved;
        public byte filter;
        public byte timing0;       // CAN 波特率 BTR0
        public byte timing1;       // CAN 波特率 BTR1
        public byte mode;          // 0=正常
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ZCAN_CHANNEL_CANFD_CFG
    {
        public uint acc_code;
        public uint acc_mask;
        public uint abit_timing;   // 仅部分老设备使用，USBCANFD 系列须用 SetValue 设置波特率
        public uint dbit_timing;
        public uint brp;
        public byte filter;
        public byte mode;          // 0=正常
        public ushort pad;
        public uint reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ZCAN_CHANNEL_INIT_CONFIG
    {
        [FieldOffset(0)] public uint can_type;      // 0=CAN, 1=CANFD
        [FieldOffset(4)] public ZCAN_CHANNEL_CAN_CFG can;
        [FieldOffset(4)] public ZCAN_CHANNEL_CANFD_CFG canfd;
    }

    // ── 报文结构（对应 canfd_frame，Linux 风格） ──────────
    [StructLayout(LayoutKind.Sequential)]
    public struct canfd_frame
    {
        public uint can_id;   // bit31: EFF, bit30: RTR, bit29: ERR
        public byte len;
        public byte flags;    // bit0: BRS, bit1: ESI
        public byte res0;
        public byte res1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct can_frame
    {
        public uint can_id;
        public byte can_dlc;
        public byte pad;
        public byte res0;
        public byte res1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ZCAN_Transmit_Data
    {
        public can_frame frame;
        public uint transmit_type; // 0=正常发送
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ZCAN_Receive_Data
    {
        public can_frame frame;
        public ulong timestamp; // 微秒
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ZCAN_TransmitFD_Data
    {
        public canfd_frame frame;
        public uint transmit_type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ZCAN_ReceiveFD_Data
    {
        public canfd_frame frame;
        public ulong timestamp; // 微秒
    }

    public const uint CAN_EFF_FLAG = 0x80000000;
    public const uint CAN_RTR_FLAG = 0x40000000;
    public const uint CAN_ERR_FLAG = 0x20000000;
    public const uint CAN_ID_MASK = 0x1FFFFFFF;
    // SDK canframe.h: __pad (Classic) / flags (FD), 回显在发送至总线后产生。
    public const byte TX_ECHO_FLAG = 0x20;
    public const byte CANFD_BRS = 0x01;

    // ── API 函数 ──────────────────────────────────────────
    [DllImport(DllName, EntryPoint = "ZCAN_OpenDevice")]
    public static extern IntPtr OpenDevice(uint deviceType, uint deviceIndex, uint reserved);

    [DllImport(DllName, EntryPoint = "ZCAN_CloseDevice")]
    public static extern uint CloseDevice(IntPtr deviceHandle);

    [DllImport(DllName, EntryPoint = "ZCAN_InitCAN")]
    public static extern IntPtr InitCAN(IntPtr deviceHandle, uint canIndex, ref ZCAN_CHANNEL_INIT_CONFIG config);

    [DllImport(DllName, EntryPoint = "ZCAN_StartCAN")]
    public static extern uint StartCAN(IntPtr channelHandle);

    [DllImport(DllName, EntryPoint = "ZCAN_ResetCAN")]
    public static extern uint ResetCAN(IntPtr channelHandle);

    [DllImport(DllName, EntryPoint = "ZCAN_ClearBuffer")]
    public static extern uint ClearBuffer(IntPtr channelHandle);

    [DllImport(DllName, EntryPoint = "ZCAN_Transmit")]
    public static extern uint Transmit(IntPtr channelHandle, ref ZCAN_Transmit_Data data, uint len);

    [DllImport(DllName, EntryPoint = "ZCAN_Receive")]
    public static extern uint Receive(IntPtr channelHandle, ref ZCAN_Receive_Data data, uint len, int waitTimeMs);

    [DllImport(DllName, EntryPoint = "ZCAN_TransmitFD")]
    public static extern uint TransmitFD(IntPtr channelHandle, ref ZCAN_TransmitFD_Data data, uint len);

    [DllImport(DllName, EntryPoint = "ZCAN_ReceiveFD")]
    public static extern uint ReceiveFD(IntPtr channelHandle, ref ZCAN_ReceiveFD_Data data, uint len, int waitTimeMs);

    [DllImport(DllName, EntryPoint = "ZCAN_Receive")]
    public static extern uint ReceiveBatch(IntPtr channelHandle,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] ZCAN_Receive_Data[] data, uint len, int waitTimeMs);

    [DllImport(DllName, EntryPoint = "ZCAN_ReceiveFD")]
    public static extern uint ReceiveFdBatch(IntPtr channelHandle,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] ZCAN_ReceiveFD_Data[] data, uint len, int waitTimeMs);

    [DllImport(DllName, EntryPoint = "ZCAN_GetReceiveNum")]
    public static extern uint GetReceiveNum(IntPtr channelHandle, byte type);

    /// <summary>设置设备属性（USBCANFD 系列须通过该接口配置波特率）</summary>
    [DllImport(DllName, EntryPoint = "ZCAN_SetValue", CharSet = CharSet.Ansi)]
    public static extern uint SetValue(IntPtr deviceHandle, string path, string value);

    [DllImport(DllName, EntryPoint = "GetIProperty")]
    private static extern IntPtr GetIProperty(IntPtr deviceHandle);

    [DllImport(DllName, EntryPoint = "ReleaseIProperty")]
    private static extern uint ReleaseIProperty(IntPtr property);

    /// <summary>zlgcan.dll 导出的属性接口（函数指针表）</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct IProperty
    {
        public IntPtr SetValue;
        public IntPtr GetValue;
        public IntPtr GetPropertys;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate uint SetValueDelegate(string path, string value);

    /// <summary>
    /// 设置设备属性。优先使用 GetIProperty 返回的 SetValue 函数指针
    /// （USBCANFD 系列的波特率属性只有该路径可靠生效），失败时回退到 ZCAN_SetValue。
    /// </summary>
    public static bool TrySetProperty(IntPtr deviceHandle, string path, string value)
    {
        IntPtr propertyPtr = IntPtr.Zero;
        try
        {
            propertyPtr = GetIProperty(deviceHandle);
            if (propertyPtr != IntPtr.Zero)
            {
                var property = Marshal.PtrToStructure<IProperty>(propertyPtr);
                if (property.SetValue != IntPtr.Zero)
                {
                    var setValue = Marshal.GetDelegateForFunctionPointer<SetValueDelegate>(property.SetValue);
                    if (setValue(path, value) == STATUS_OK)
                        return true;
                }
            }
        }
        catch (EntryPointNotFoundException)
        {
            // 旧版 zlgcan.dll 未导出 GetIProperty，走下面的回退分支
        }
        finally
        {
            if (propertyPtr != IntPtr.Zero)
            {
                try { ReleaseIProperty(propertyPtr); } catch (EntryPointNotFoundException) { }
            }
        }

        try
        {
            return SetValue(deviceHandle, path, value) == STATUS_OK;
        }
        catch (EntryPointNotFoundException)
        {
            // 部分旧版 DLL 只有 GetIProperty 属性接口。
            return false;
        }
    }

    /// <summary>判断设备是否支持 CAN FD。</summary>
    public static bool IsCanFdDevice(uint deviceType) =>
        deviceType is ZCAN_USBCANFD_200U or ZCAN_USBCANFD_100U or ZCAN_USBCANFD_MINI ||
        IsPcieCanFdDevice(deviceType);

    public static bool IsPcieCanFdDevice(uint deviceType) => deviceType is
        ZCAN_PCIE_CANFD_100U or ZCAN_PCIE_CANFD_200U or ZCAN_PCIE_CANFD_400U or
        ZCAN_PCIE_CANFD_100U_EX or ZCAN_PCIE_CANFD_400U_EX or
        ZCAN_PCIE_CANFD_200U_MINI or ZCAN_PCIE_CANFD_200U_EX or
        ZCAN_PCIE_CANFD_800U or ZCAN_PCIE_CANFD_1200U or ZCAN_MINI_PCIE_CANFD;

    // 这些型号的 SDK XML 仅提供 protocol=1；Classic 报文也使用 CAN FD 控制器初始化。
    public static bool UsesCanFdOnlyController(uint deviceType) =>
        IsPcieCanFdDevice(deviceType) && deviceType is not
            (ZCAN_PCIE_CANFD_100U or ZCAN_PCIE_CANFD_200U or ZCAN_PCIE_CANFD_400U);

    public static bool UsesDriverClassicBitRate(uint deviceType) =>
        deviceType is ZCAN_PCI5010U or ZCAN_PCI5020U;

    public static double GetStandardDataSamplePoint(uint deviceType, int bitRate) =>
        IsPcieCanFdDevice(deviceType) && bitRate == 5_000_000 ? 75d : 80d;

    public static void ValidatePcieBitRates(uint deviceType, int arbitrationBitRate, int? dataBitRate)
    {
        bool extendedRates = UsesCanFdOnlyController(deviceType);
        if (arbitrationBitRate is not (1_000_000 or 800_000 or 500_000 or 250_000 or 125_000) &&
            !(extendedRates && arbitrationBitRate is (100_000 or 50_000)))
        {
            throw new ArgumentException(
                $"当前 PCIe CAN FD 型号的 SDK 不支持 {arbitrationBitRate} bps 标准仲裁段波特率；" +
                "请使用设备 XML 中的标准波特率，或提供厂商自定义时序规则。");
        }
        if (dataBitRate.HasValue &&
            dataBitRate.Value is not (5_000_000 or 4_000_000 or 2_000_000 or 1_000_000) &&
            !(extendedRates && dataBitRate.Value is (800_000 or 500_000 or 250_000)))
        {
            throw new ArgumentException(
                $"当前 PCIe CAN FD 型号的 SDK 不支持 {dataBitRate} bps 标准数据段波特率；" +
                "请使用设备 XML 中的标准波特率，或提供厂商自定义时序规则。");
        }
    }

    public static string GetDeviceOpenDiagnostics(uint deviceType)
    {
        string? kernelDll = deviceType switch
        {
            ZCAN_PCI5121 or ZCAN_PCI5110 => "PCI51XXE.dll",
            ZCAN_PCI9810 => "PCI9810.dll",
            ZCAN_PCI9820 => "PCI9820.dll",
            ZCAN_PCI9840 => "PCI9840B.dll",
            ZCAN_PCI9820I => "PCI9820I.dll",
            ZCAN_PCI5010U or ZCAN_PCI5020U => "pci50xx_u.dll",
            ZCAN_PCIE9221 => "pcie9221.dll",
            ZCAN_PCIE9120 => "pcie9120.dll",
            ZCAN_PCIE9110 => "pcie9110.dll",
            ZCAN_PCIE9140 => "pcie9140.dll",
            ZCAN_PCIE_CANFD_100U or ZCAN_PCIE_CANFD_200U or ZCAN_PCIE_CANFD_400U => "zpcfd.dll",
            _ when IsPcieCanFdDevice(deviceType) => "xpcfd.dll",
            _ => null
        };
        if (deviceType == ZCAN_PCIE_9220)
            return "当前 SDK 的 dll_cfg.ini 未配置 PCIe9220，请使用包含该型号的官方 x64 二次开发库。";
        if (kernelDll is null)
            return "";
        if (NativeDir is null)
            return $"请确认官方 x64 二次开发库的 kerneldlls 中包含 {kernelDll}。";

        string path = Path.Combine(NativeDir, "kerneldlls", kernelDll);
        return File.Exists(path)
            ? $"板卡库已找到：{path}；请检查其依赖和 Windows 板卡驱动。"
            : $"插件原生库目录缺少板卡库：{path}；请补充官方 x64 二次开发包（保持 kerneldlls 目录结构）。";
    }

    /// <summary>设备类型名转设备类型码</summary>
    public static uint ParseDeviceType(string name)
    {
        string normalized = name.Trim().ToUpperInvariant();
        if (normalized.StartsWith("ZCAN_", StringComparison.Ordinal))
            normalized = normalized[5..];
        return normalized.Replace('_', '-') switch
        {
            "USBCAN1" or "USBCAN-I" => ZCAN_USBCAN1,
            "USBCAN2" or "USBCAN-II" => ZCAN_USBCAN2,
            "USBCAN-E-U" => ZCAN_USBCAN_E_U,
            "USBCAN-2E-U" => ZCAN_USBCAN_2E_U,
            "USBCANFD-200U" => ZCAN_USBCANFD_200U,
            "USBCANFD-100U" => ZCAN_USBCANFD_100U,
            "USBCANFD-MINI" => ZCAN_USBCANFD_MINI,
            "PCI5121" or "PCI-5121" => ZCAN_PCI5121,
            "PCI9810" or "PCI-9810" => ZCAN_PCI9810,
            "PCI9820" or "PCI-9820" => ZCAN_PCI9820,
            "PCI5110" or "PCI-5110" => ZCAN_PCI5110,
            "PCI9840" or "PCI-9840" => ZCAN_PCI9840,
            "PCI9820I" or "PCI-9820I" => ZCAN_PCI9820I,
            "PCIE9220" or "PCIE-9220" => ZCAN_PCIE_9220,
            "PCI5010U" or "PCI-5010-U" => ZCAN_PCI5010U,
            "PCI5020U" or "PCI-5020-U" => ZCAN_PCI5020U,
            "PCIE9221" or "PCIE-9221" => ZCAN_PCIE9221,
            "PCIE9120" or "PCIE-9120" => ZCAN_PCIE9120,
            "PCIE9110" or "PCIE-9110" => ZCAN_PCIE9110,
            "PCIE9140" or "PCIE-9140" => ZCAN_PCIE9140,
            "PCIE-CANFD-100U" => ZCAN_PCIE_CANFD_100U,
            "PCIE-CANFD-200U" => ZCAN_PCIE_CANFD_200U,
            "PCIE-CANFD-400U" => ZCAN_PCIE_CANFD_400U,
            "PCIE-CANFD-100U-EX" => ZCAN_PCIE_CANFD_100U_EX,
            "PCIE-CANFD-400U-EX" => ZCAN_PCIE_CANFD_400U_EX,
            "PCIE-CANFD-200U-MINI" => ZCAN_PCIE_CANFD_200U_MINI,
            "PCIE-CANFD-200U-EX" or "PCIE-CANFD-200U-M2" => ZCAN_PCIE_CANFD_200U_EX,
            "PCIE-CANFD-800U" => ZCAN_PCIE_CANFD_800U,
            "PCIE-CANFD-1200U" => ZCAN_PCIE_CANFD_1200U,
            "MINI-PCIE-CANFD" => ZCAN_MINI_PCIE_CANFD,
            _ => throw new ArgumentException(
                $"未知的 ZLG 设备类型 '{name}'，支持已接入的 USBCAN、USBCANFD、PCI 和 PCIe 型号；完整列表请查看 ZLG 原生库 README。")
        };
    }

    /// <summary>Classic 波特率和采样点转 SJA1000 BTR0/BTR1 编码。</summary>
    public static (byte Timing0, byte Timing1) ToTiming(int baudRate, double samplePoint)
    {
        ushort btr = CanSamplePointCalculator.ToSja1000Btr(baudRate, samplePoint);
        return ((byte)(btr >> 8), (byte)btr);
    }
}
