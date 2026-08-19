using System.Runtime.InteropServices;

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

    /// <summary>生成原生库加载失败的诊断说明</summary>
    internal static string GetLoadDiagnostics()
    {
        var dirs = ProbedDirs.Count == 0 ? "（无）" : string.Join("；", ProbedDirs);

        if (NativeDir is null)
            return $"未在以下任何位置找到 {DllName}：{dirs}";

        var kernelDir = Path.Combine(NativeDir, "kerneldlls");
        var detail = LoadFailure is null
            ? "未进入插件自带的加载流程（请确认运行的插件为最新版本）"
            : $"加载失败：{LoadFailure}";

        return $"已定位原生库目录：{NativeDir}（kerneldlls 存在={Directory.Exists(kernelDir)}）；{detail}；已探测：{dirs}";
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

    // ── 帧类型 ────────────────────────────────────────────
    public const uint ZCAN_TYPE_CAN = 0;
    public const uint ZCAN_TYPE_CANFD = 1;

    // ── 初始化配置（union 简化为 CAN/CANFD 通用布局） ─────
    [StructLayout(LayoutKind.Sequential)]
    public struct ZCAN_CHANNEL_INIT_CONFIG
    {
        public uint can_type;      // 0=CAN, 1=CANFD
        public uint acc_code;
        public uint acc_mask;
        public uint abit_timing;   // CANFD 仲裁段时序（或 CAN 保留）
        public uint dbit_timing;   // CANFD 数据段时序
        public uint brp;
        public byte filter;
        public byte timing0;       // CAN 波特率 BTR0
        public byte timing1;       // CAN 波特率 BTR1
        public byte mode;          // 0=正常
        public uint pad;
        public uint reserved;
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

    [DllImport(DllName, EntryPoint = "ZCAN_Transmit")]
    public static extern uint Transmit(IntPtr channelHandle, ref ZCAN_Transmit_Data data, uint len);

    [DllImport(DllName, EntryPoint = "ZCAN_Receive")]
    public static extern uint Receive(IntPtr channelHandle, ref ZCAN_Receive_Data data, uint len, int waitTimeMs);

    [DllImport(DllName, EntryPoint = "ZCAN_TransmitFD")]
    public static extern uint TransmitFD(IntPtr channelHandle, ref ZCAN_TransmitFD_Data data, uint len);

    [DllImport(DllName, EntryPoint = "ZCAN_ReceiveFD")]
    public static extern uint ReceiveFD(IntPtr channelHandle, ref ZCAN_ReceiveFD_Data data, uint len, int waitTimeMs);

    [DllImport(DllName, EntryPoint = "ZCAN_GetReceiveNum")]
    public static extern uint GetReceiveNum(IntPtr channelHandle, byte type);

    /// <summary>设备类型名转设备类型码</summary>
    public static uint ParseDeviceType(string name) => name.Trim().ToUpperInvariant() switch
    {
        "USBCAN1" or "USBCAN-I" => ZCAN_USBCAN1,
        "USBCAN2" or "USBCAN-II" => ZCAN_USBCAN2,
        "USBCAN-E-U" => ZCAN_USBCAN_E_U,
        "USBCAN-2E-U" => ZCAN_USBCAN_2E_U,
        "USBCANFD-200U" => ZCAN_USBCANFD_200U,
        "USBCANFD-100U" => ZCAN_USBCANFD_100U,
        "USBCANFD-MINI" => ZCAN_USBCANFD_MINI,
        _ => throw new ArgumentException(
            $"未知的 ZLG 设备类型 '{name}'，支持：USBCAN1, USBCAN2, USBCAN-E-U, USBCAN-2E-U, USBCANFD-200U, USBCANFD-100U, USBCANFD-MINI")
    };

    /// <summary>Classic 波特率（bps）转 BTR0/BTR1 编码</summary>
    public static (byte Timing0, byte Timing1) ToTiming(int baudRate) => baudRate switch
    {
        1_000_000 => ((byte)0x00, (byte)0x14),
        800_000 => ((byte)0x00, (byte)0x16),
        500_000 => ((byte)0x00, (byte)0x1C),
        250_000 => ((byte)0x01, (byte)0x1C),
        125_000 => ((byte)0x03, (byte)0x1C),
        100_000 => ((byte)0x04, (byte)0x39),
        50_000 => ((byte)0x09, (byte)0x1C),
        20_000 => ((byte)0x18, (byte)0x1C),
        10_000 => ((byte)0x31, (byte)0x1C),
        _ => throw new ArgumentException($"ZLG 不支持的波特率 {baudRate} bps")
    };
}
