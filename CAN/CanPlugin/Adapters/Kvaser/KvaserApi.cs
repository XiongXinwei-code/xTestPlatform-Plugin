using System.Runtime.InteropServices;
using System.Text;

namespace CAN.Adapters.Kvaser;

/// <summary>Kvaser CANlib P/Invoke 封装（canlib32.dll，64 位系统同名）</summary>
internal static class KvaserApi
{
    private const string DllName = "canlib32.dll";

    // ── 状态码 ────────────────────────────────────────────
    public const int canOK = 0;
    public const int canERR_NOMSG = -2;

    // ── 打开通道标志 ──────────────────────────────────────
    public const int canOPEN_ACCEPT_VIRTUAL = 0x0020;
    public const int canOPEN_CAN_FD = 0x0400;

    // ── 预定义波特率常量（Classic） ───────────────────────
    public const int canBITRATE_1M = -1;
    public const int canBITRATE_500K = -2;
    public const int canBITRATE_250K = -3;
    public const int canBITRATE_125K = -4;
    public const int canBITRATE_100K = -5;
    public const int canBITRATE_62K = -6;
    public const int canBITRATE_50K = -7;
    public const int canBITRATE_83K = -8;
    public const int canBITRATE_10K = -9;

    // ── 预定义波特率常量（FD） ────────────────────────────
    public const int canFD_BITRATE_500K_80P = -1000;
    public const int canFD_BITRATE_1M_80P = -1001;
    public const int canFD_BITRATE_2M_80P = -1002;
    public const int canFD_BITRATE_4M_80P = -1003;
    public const int canFD_BITRATE_8M_60P = -1004;
    public const int canFD_BITRATE_8M_80P = -1007;

    // ── 消息标志 ──────────────────────────────────────────
    public const uint canMSG_EXT = 0x0004;
    public const uint canMSG_STD = 0x0002;
    public const uint canFDMSG_FDF = 0x010000;
    public const uint canFDMSG_BRS = 0x020000;

    // ── API 函数 ──────────────────────────────────────────
    [DllImport(DllName, EntryPoint = "canInitializeLibrary")]
    public static extern void InitializeLibrary();

    [DllImport(DllName, EntryPoint = "canOpenChannel")]
    public static extern int OpenChannel(int channel, int flags);

    [DllImport(DllName, EntryPoint = "canClose")]
    public static extern int Close(int handle);

    [DllImport(DllName, EntryPoint = "canSetBusParams")]
    public static extern int SetBusParams(int handle, int freq, uint tseg1, uint tseg2, uint sjw, uint noSamp, uint syncmode);

    [DllImport(DllName, EntryPoint = "canSetBusParamsFd")]
    public static extern int SetBusParamsFd(int handle, int freqDbr, uint tseg1Dbr, uint tseg2Dbr, uint sjwDbr);

    [DllImport(DllName, EntryPoint = "canBusOn")]
    public static extern int BusOn(int handle);

    [DllImport(DllName, EntryPoint = "canBusOff")]
    public static extern int BusOff(int handle);

    [DllImport(DllName, EntryPoint = "canWrite")]
    public static extern int Write(int handle, int id, byte[] msg, uint dlc, uint flag);

    [DllImport(DllName, EntryPoint = "canWriteWait")]
    public static extern int WriteWait(int handle, int id, byte[] msg, uint dlc, uint flag, uint timeoutMs);

    [DllImport(DllName, EntryPoint = "canReadWait")]
    public static extern int ReadWait(int handle, out int id, byte[] msg, out uint dlc, out uint flag, out uint time, uint timeoutMs);

    [DllImport(DllName, EntryPoint = "canGetErrorText")]
    public static extern int GetErrorText(int err, StringBuilder buf, uint bufsiz);

    /// <summary>检查 CANlib 返回状态码，非 canOK 则抛出异常</summary>
    public static void CheckStatus(int status)
    {
        if (status >= canOK) return;
        var sb = new StringBuilder(256);
        GetErrorText(status, sb, (uint)sb.Capacity);
        throw new InvalidOperationException($"Kvaser CANlib 错误 ({status}): {sb}");
    }

    /// <summary>仲裁段波特率（bps）转 CANlib 预定义常量</summary>
    public static int ToBitrateConst(int baudRate) => baudRate switch
    {
        1_000_000 => canBITRATE_1M,
        500_000 => canBITRATE_500K,
        250_000 => canBITRATE_250K,
        125_000 => canBITRATE_125K,
        100_000 => canBITRATE_100K,
        62_000 => canBITRATE_62K,
        50_000 => canBITRATE_50K,
        10_000 => canBITRATE_10K,
        _ => throw new ArgumentException($"Kvaser 不支持的波特率 {baudRate} bps")
    };

    /// <summary>FD 数据段波特率（bps）转 CANlib FD 预定义常量（采样点 80%）</summary>
    public static int ToFdDataBitrateConst(int dataBitRate) => dataBitRate switch
    {
        500_000 => canFD_BITRATE_500K_80P,
        1_000_000 => canFD_BITRATE_1M_80P,
        2_000_000 => canFD_BITRATE_2M_80P,
        4_000_000 => canFD_BITRATE_4M_80P,
        8_000_000 => canFD_BITRATE_8M_80P,
        _ => throw new ArgumentException($"Kvaser FD 不支持的数据段波特率 {dataBitRate} bps（支持 500K/1M/2M/4M/8M）")
    };
}
