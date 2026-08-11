using System.Runtime.InteropServices;

namespace CAN.Adapters.Tosun;

/// <summary>同星 TOSUN TSCAN API P/Invoke 封装（libTSCAN.dll）</summary>
internal static class TosunApi
{
    private const string DllName = "libTSCAN.dll";

    // ── 返回值 ────────────────────────────────────────────
    public const uint TSCAN_OK = 0;

    // ── 帧标识 ────────────────────────────────────────────
    public const byte PROP_STANDARD_DATA = 0x00; // 标准数据帧
    public const byte PROP_EXTENDED_DATA = 0x04; // 扩展数据帧（IDF 位）
    public const byte PROP_TX = 0x01;            // 发送方向

    // ── CANFD 标志 ────────────────────────────────────────
    public const byte FD_PROP_BRS = 0x01;

    // ── Classic CAN 报文结构（TLibCAN） ───────────────────
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TLibCAN
    {
        public byte FIdxChn;      // 通道索引
        public byte FProperties;  // bit0: dir(1=Tx), bit2: IDF(1=扩展帧), bit1: RMT
        public byte FDLC;
        public byte FReserved;
        public int FIdentifier;
        public ulong FTimeUs;     // 时间戳（微秒）
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] FData;
    }

    // ── CANFD 报文结构（TLibCANFD） ───────────────────────
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TLibCANFD
    {
        public byte FIdxChn;
        public byte FProperties;   // 同 TLibCAN
        public byte FDLC;          // FD DLC 编码 0~15
        public byte FFDProperties; // bit0: BRS, bit1: ESI
        public int FIdentifier;
        public ulong FTimeUs;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] FData;
    }

    // ── API 函数 ──────────────────────────────────────────
    [DllImport(DllName, EntryPoint = "initialize_lib_tscan")]
    public static extern void InitializeLib(bool enableFifo, bool enableTurbo);

    [DllImport(DllName, EntryPoint = "finalize_lib_tscan")]
    public static extern void FinalizeLib();

    [DllImport(DllName, EntryPoint = "tscan_scan_devices")]
    public static extern uint ScanDevices(ref uint deviceCount);

    [DllImport(DllName, EntryPoint = "tscan_connect", CharSet = CharSet.Ansi)]
    public static extern uint Connect(string ip, ref nuint deviceHandle);

    [DllImport(DllName, EntryPoint = "tscan_disconnect_by_handle")]
    public static extern uint Disconnect(nuint deviceHandle);

    [DllImport(DllName, EntryPoint = "tscan_config_can_by_baudrate")]
    public static extern uint ConfigCanByBaudrate(nuint deviceHandle, int channelIdx, double baudRateKbps, uint enable120Ohm);

    [DllImport(DllName, EntryPoint = "tscan_config_canfd_by_baudrate")]
    public static extern uint ConfigCanFdByBaudrate(nuint deviceHandle, int channelIdx,
        double arbBaudRateKbps, double dataBaudRateKbps, byte controllerType, byte controllerMode, uint enable120Ohm);

    [DllImport(DllName, EntryPoint = "tscan_transmit_can_sync")]
    public static extern uint TransmitCanSync(nuint deviceHandle, ref TLibCAN msg, uint timeoutMs);

    [DllImport(DllName, EntryPoint = "tscan_transmit_canfd_sync")]
    public static extern uint TransmitCanFdSync(nuint deviceHandle, ref TLibCANFD msg, uint timeoutMs);

    [DllImport(DllName, EntryPoint = "tsfifo_receive_can_msgs")]
    public static extern uint ReceiveCanMsgs(nuint deviceHandle, [In, Out] TLibCAN[] buffer, ref int size, int channelIdx, byte rxTx);

    [DllImport(DllName, EntryPoint = "tsfifo_receive_canfd_msgs")]
    public static extern uint ReceiveCanFdMsgs(nuint deviceHandle, [In, Out] TLibCANFD[] buffer, ref int size, int channelIdx, byte rxTx);

    /// <summary>检查 TSCAN 返回状态码，非 0 则抛出异常</summary>
    public static void CheckStatus(uint status, string operation)
    {
        if (status == TSCAN_OK) return;
        throw new InvalidOperationException($"TOSUN TSCAN 错误 ({status})：{operation} 失败");
    }
}
