using System.Runtime.InteropServices;

namespace CAN.Adapters.Vector;

/// <summary>Vector XL Driver Library P/Invoke 封装（vxlapi64.dll）</summary>
internal static class VectorXlApi
{
    private const string DllName = "vxlapi64.dll";

    // ── 原生库加载 ────────────────────────────────────────────────────────────
    // 插件由宿主动态加载，DllImport 默认搜索的是宿主进程目录而非插件目录，
    // 因此这里把随插件一起发布的 Native\Vector 目录显式加入搜索路径。
    // 若该目录下没有 vxlapi64.dll（例如未随包发布），解析器返回 Zero，
    // 运行时会退回系统默认搜索顺序，即使用现场已安装的 Vector 驱动。
    static VectorXlApi()
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
                NativeLibrary.SetDllImportResolver(typeof(VectorXlApi).Assembly, ResolveNativeLibrary);
                break;
            }
        }
        catch (Exception ex)
        {
            NativeDir = null;
            tried.Add($"[异常] {ex.Message}");
        }

        ProbedDirs = tried;
    }

    /// <summary>依次列出可能存放 Native\Vector 的目录</summary>
    private static IEnumerable<string?> EnumerateCandidateDirs()
    {
        // 1) 插件程序集自身所在目录（正常从文件加载时）
        string? asmDir = null;
        try
        {
            var loc = typeof(VectorXlApi).Assembly.Location;
            if (!string.IsNullOrEmpty(loc))
                asmDir = Path.GetDirectoryName(loc);
        }
        catch { /* 忽略 */ }
        if (asmDir is not null)
            yield return Path.Combine(asmDir, "Native", "Vector");

        // 2) 宿主基目录下的插件目录（Assembly.Location 为空时的兜底）
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            yield return Path.Combine(baseDir, "Plugins", "CAN", "Native", "Vector");
            yield return Path.Combine(baseDir, "Native", "Vector");

            // 3) 插件目录名可能与宿主约定不同，扫描 Plugins 下一层
            var pluginsRoot = Path.Combine(baseDir, "Plugins");
            string[] subDirs;
            try { subDirs = Directory.Exists(pluginsRoot) ? Directory.GetDirectories(pluginsRoot) : []; }
            catch { subDirs = []; }
            foreach (var sub in subDirs)
                yield return Path.Combine(sub, "Native", "Vector");
        }
    }

    /// <summary>实际使用的原生库目录，未随包发布时为 null（走系统已安装驱动）</summary>
    private static readonly string? NativeDir;

    /// <summary>探测过的目录，用于加载失败时输出诊断信息</summary>
    private static readonly IReadOnlyList<string> ProbedDirs;

    /// <summary>插件自带库加载失败的原因，成功或未使用时为 null</summary>
    private static string? LoadFailure;

    /// <summary>生成原生库加载失败的诊断说明</summary>
    internal static string GetLoadDiagnostics()
    {
        var dirs = ProbedDirs.Count == 0 ? "（无）" : string.Join("；", ProbedDirs);
        if (NativeDir is null)
            return $"插件目录下未附带 {DllName}，且系统中也未找到（依赖已安装的 Vector 驱动）。已探测：{dirs}";

        var detail = LoadFailure is null ? "未进入插件自带的加载流程" : $"加载失败：{LoadFailure}";
        return $"已定位插件自带原生库目录：{NativeDir}；{detail}；已探测：{dirs}";
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (NativeDir is null || !string.Equals(libraryName, DllName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        try
        {
            return NativeLibrary.Load(Path.Combine(NativeDir, DllName));
        }
        catch (Exception ex)
        {
            // 回退到系统默认搜索顺序（现场已安装的 Vector 驱动）
            LoadFailure = $"{ex.GetType().Name}: {ex.Message}";
            return IntPtr.Zero;
        }
    }

    // ── 状态码 ────────────────────────────────────────────
    public const short XL_SUCCESS = 0;
    public const short XL_ERR_QUEUE_IS_EMPTY = 10;

    // ── 总线类型 ──────────────────────────────────────────
    public const uint XL_BUS_TYPE_CAN = 0x00000001;

    // ── 接口版本 ──────────────────────────────────────────
    public const uint XL_INTERFACE_VERSION = 3;   // Classic CAN
    public const uint XL_INTERFACE_VERSION_V4 = 4; // CAN FD

    // ── 激活标志 ──────────────────────────────────────────
    public const uint XL_ACTIVATE_RESET_CLOCK = 8;

    // ── 事件标签 ──────────────────────────────────────────
    public const byte XL_RECEIVE_MSG = 1;
    public const ushort XL_CAN_EV_TAG_RX_OK = 0x0400;
    public const ushort XL_CAN_EV_TAG_TX_OK = 0x0404;

    // ── CAN 消息标志 ──────────────────────────────────────
    public const uint XL_CAN_EXT_MSG_ID = 0x80000000;
    public const uint XL_CAN_TXMSG_FLAG_EDL = 0x0001; // FD 帧
    public const uint XL_CAN_TXMSG_FLAG_BRS = 0x0002; // 位速率切换
    public const uint XL_CAN_RXMSG_FLAG_EDL = 0x0001;
    public const uint XL_CAN_RXMSG_FLAG_BRS = 0x0002;

    // ── Classic 事件结构 ──────────────────────────────────
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct XLcanMsg
    {
        public uint id;
        public ushort flags;
        public ushort dlc;
        public ulong res1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] data;
        public ulong res2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct XLevent
    {
        public byte tag;
        public byte chanIndex;
        public ushort transId;
        public ushort portHandle;
        public byte flags;
        public byte reserved;
        public ulong timeStamp;
        public XLcanMsg tagData; // union 中仅使用 CAN msg
    }

    // ── CAN FD 结构 ───────────────────────────────────────
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct XLcanTxEvent
    {
        public ushort tag;         // XL_CAN_EV_TAG_TX_MSG = 0x0440
        public ushort transId;
        public byte channelIndex;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] reserved;
        public uint canId;
        public uint msgFlags;
        public byte dlc;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public byte[] reserved1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] data;
    }

    public const ushort XL_CAN_EV_TAG_TX_MSG = 0x0440;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct XLcanRxEvent
    {
        public uint size;
        public ushort tag;
        public ushort channelIndex;
        public uint userHandle;
        public ushort flagsChip;
        public ushort reserved0;
        public ulong reserved1;
        public ulong timeStampSync;
        // union: XL_CAN_EV_RX_MSG
        public uint canId;
        public uint msgFlags;
        public uint crc;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] reserved3;
        // vxlapi.h 中 totalBitCnt 为 unsigned short；若按 1 字节声明会使 dlc 与 data 整体错位。
        public ushort totalBitCnt;
        public byte dlc;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] reserved4;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] data;
    }

    // ── FD 位速率配置 ─────────────────────────────────────
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct XLcanFdConf
    {
        public uint arbitrationBitRate;
        public uint sjwAbr;
        public uint tseg1Abr;
        public uint tseg2Abr;
        public uint dataBitRate;
        public uint sjwDbr;
        public uint tseg1Dbr;
        public uint tseg2Dbr;
        public byte reserved;
        public byte options;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] reserved1;
        public uint reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct XLchipParams
    {
        public uint bitRate;
        public byte sjw;
        public byte tseg1;
        public byte tseg2;
        public byte sam;
    }

    // ── API 函数 ──────────────────────────────────────────
    [DllImport(DllName, EntryPoint = "xlOpenDriver")]
    public static extern short OpenDriver();

    [DllImport(DllName, EntryPoint = "xlCloseDriver")]
    public static extern short CloseDriver();

    [DllImport(DllName, EntryPoint = "xlGetChannelMask")]
    public static extern ulong GetChannelMask(int hwType, int hwIndex, int hwChannel);

    [DllImport(DllName, EntryPoint = "xlOpenPort", CharSet = CharSet.Ansi)]
    public static extern short OpenPort(ref int portHandle, string userName, ulong accessMask,
        ref ulong permissionMask, uint rxQueueSize, uint xlInterfaceVersion, uint busType);

    [DllImport(DllName, EntryPoint = "xlClosePort")]
    public static extern short ClosePort(int portHandle);

    [DllImport(DllName, EntryPoint = "xlActivateChannel")]
    public static extern short ActivateChannel(int portHandle, ulong accessMask, uint busType, uint flags);

    [DllImport(DllName, EntryPoint = "xlDeactivateChannel")]
    public static extern short DeactivateChannel(int portHandle, ulong accessMask);

    [DllImport(DllName, EntryPoint = "xlCanSetChannelBitrate")]
    public static extern short CanSetChannelBitrate(int portHandle, ulong accessMask, uint bitrate);

    [DllImport(DllName, EntryPoint = "xlCanSetChannelParams")]
    public static extern short CanSetChannelParams(
        int portHandle, ulong accessMask, ref XLchipParams chipParams);

    [DllImport(DllName, EntryPoint = "xlCanFdSetConfiguration")]
    public static extern short CanFdSetConfiguration(int portHandle, ulong accessMask, ref XLcanFdConf conf);

    [DllImport(DllName, EntryPoint = "xlCanTransmit")]
    public static extern short CanTransmit(int portHandle, ulong accessMask, ref uint messageCount, ref XLevent messages);

    [DllImport(DllName, EntryPoint = "xlCanTransmitEx")]
    public static extern short CanTransmitEx(int portHandle, ulong accessMask, uint msgCnt, ref uint msgCntSent, ref XLcanTxEvent messages);

    [DllImport(DllName, EntryPoint = "xlReceive")]
    public static extern short Receive(int portHandle, ref uint eventCount, ref XLevent events);

    [DllImport(DllName, EntryPoint = "xlCanReceive")]
    public static extern short CanReceive(int portHandle, ref XLcanRxEvent xlCanRxEvt);

    [DllImport(DllName, EntryPoint = "xlGetErrorString")]
    public static extern IntPtr GetErrorString(short err);

    /// <summary>检查 XL API 返回状态码，非 XL_SUCCESS 则抛出异常</summary>
    public static void CheckStatus(short status)
    {
        if (status == XL_SUCCESS) return;
        var msg = Marshal.PtrToStringAnsi(GetErrorString(status)) ?? "Unknown";
        throw new InvalidOperationException($"Vector XL 错误 ({status}): {msg}");
    }
}
