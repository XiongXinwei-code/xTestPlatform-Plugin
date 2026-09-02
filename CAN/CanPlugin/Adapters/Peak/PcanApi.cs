using System.Runtime.InteropServices;
using System.Text;
using CAN.Helpers;

namespace CAN.Adapters.Peak;

/// <summary>PCAN-Basic API P/Invoke 封装（PCANBasic.dll）</summary>
internal static class PcanApi
{
    private const string DllName = "PCANBasic.dll";

    // ── 通道句柄 ──────────────────────────────────────────
    public const ushort PCAN_USBBUS1 = 0x51;

    // ── 状态码 ────────────────────────────────────────────
    public const uint PCAN_ERROR_OK = 0x00000;
    public const uint PCAN_ERROR_QRCVEMPTY = 0x00020;

    // ── 波特率（Classic，BTR0BTR1 编码） ──────────────────
    public const ushort PCAN_BAUD_1M = 0x0014;
    public const ushort PCAN_BAUD_800K = 0x0016;
    public const ushort PCAN_BAUD_500K = 0x001C;
    public const ushort PCAN_BAUD_250K = 0x011C;
    public const ushort PCAN_BAUD_125K = 0x031C;
    public const ushort PCAN_BAUD_100K = 0x432F;
    public const ushort PCAN_BAUD_50K = 0x472F;
    public const ushort PCAN_BAUD_20K = 0x532F;
    public const ushort PCAN_BAUD_10K = 0x672F;
    public const ushort PCAN_BAUD_5K = 0x7F7F;

    // ── 消息类型标志 ──────────────────────────────────────
    public const byte PCAN_MESSAGE_STANDARD = 0x00;
    public const byte PCAN_MESSAGE_RTR = 0x01;
    public const byte PCAN_MESSAGE_EXTENDED = 0x02;
    public const byte PCAN_MESSAGE_FD = 0x04;
    public const byte PCAN_MESSAGE_BRS = 0x08;

    // ── Classic 报文结构 ──────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    public struct TPCANMsg
    {
        public uint ID;
        public byte MSGTYPE;
        public byte LEN;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] DATA;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TPCANTimestamp
    {
        public uint millis;
        public ushort millis_overflow;
        public ushort micros;
    }

    // ── FD 报文结构 ───────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    public struct TPCANMsgFD
    {
        public uint ID;
        public byte MSGTYPE;
        public byte DLC;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] DATA;
    }

    // ── API 函数 ──────────────────────────────────────────
    [DllImport(DllName, EntryPoint = "CAN_Initialize")]
    public static extern uint Initialize(ushort channel, ushort btr0btr1, byte hwType, uint ioPort, ushort interrupt);

    [DllImport(DllName, EntryPoint = "CAN_InitializeFD")]
    public static extern uint InitializeFD(ushort channel, [MarshalAs(UnmanagedType.LPStr)] string bitrateFd);

    [DllImport(DllName, EntryPoint = "CAN_Uninitialize")]
    public static extern uint Uninitialize(ushort channel);

    [DllImport(DllName, EntryPoint = "CAN_Read")]
    public static extern uint Read(ushort channel, out TPCANMsg message, out TPCANTimestamp timestamp);

    [DllImport(DllName, EntryPoint = "CAN_ReadFD")]
    public static extern uint ReadFD(ushort channel, out TPCANMsgFD message, out ulong timestamp);

    [DllImport(DllName, EntryPoint = "CAN_Write")]
    public static extern uint Write(ushort channel, ref TPCANMsg message);

    [DllImport(DllName, EntryPoint = "CAN_WriteFD")]
    public static extern uint WriteFD(ushort channel, ref TPCANMsgFD message);

    [DllImport(DllName, EntryPoint = "CAN_GetErrorText")]
    public static extern uint GetErrorText(uint error, ushort language, StringBuilder buffer);

    /// <summary>检查 PCAN 返回状态码，非 OK 则抛出异常</summary>
    public static void CheckStatus(uint status)
    {
        if (status == PCAN_ERROR_OK) return;
        var sb = new StringBuilder(256);
        GetErrorText(status, 0, sb);
        throw new InvalidOperationException($"PCAN 错误 (0x{status:X}): {sb}");
    }

    /// <summary>通道名（如 PCAN_USBBUS1）转通道句柄</summary>
    public static ushort ParseChannel(string channel)
    {
        // 支持 PCAN_USBBUS1 ~ PCAN_USBBUS16，或直接数字 1~16
        var name = channel.Trim();
        if (name.StartsWith("PCAN_USBBUS", StringComparison.OrdinalIgnoreCase))
            name = name["PCAN_USBBUS".Length..];
        if (int.TryParse(name, out int index) && index >= 1 && index <= 16)
            return (ushort)(PCAN_USBBUS1 + index - 1);
        throw new ArgumentException($"无效的 PEAK 通道名 '{channel}'，应为 PCAN_USBBUS1~PCAN_USBBUS16 或数字 1~16");
    }

    /// <summary>Classic 波特率和目标采样点转 SJA1000 BTR0BTR1 编码。</summary>
    public static ushort ToBtr0Btr1(int baudRate, double samplePoint) =>
        CanSamplePointCalculator.ToSja1000Btr(baudRate, samplePoint);
}
