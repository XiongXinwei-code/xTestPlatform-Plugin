using System.Runtime.InteropServices;

namespace CAN.Adapters.Vector;

/// <summary>Vector XL Driver Library P/Invoke 封装（vxlapi64.dll）</summary>
internal static class VectorXlApi
{
    private const string DllName = "vxlapi64.dll";

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
        public byte totalBitCnt;
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
