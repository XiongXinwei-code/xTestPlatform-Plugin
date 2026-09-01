using System.Runtime.InteropServices;

namespace CAN.Adapters.NiXnet;

/// <summary>NI-XNET C API P/Invoke 封装</summary>
internal static class NiXnetApi
{
    private const string DllName = "nixnet.dll";

    // ── 会话管理 ────────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxCreateSession(
        string databaseName,
        string clusterName,
        string list,
        string interfaceName,
        uint mode,
        out uint sessionRef);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxClear(uint sessionRef);

    // ── 启动/停止 ───────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxStart(uint sessionRef, uint scope);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxStop(uint sessionRef, uint scope);

    // ── 读写帧 ──────────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxReadFrame(
        uint sessionRef,
        [Out] byte[] buffer,
        uint sizeOfBuffer,
        double timeout,
        out uint numberOfBytesReturned);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxWriteFrame(
        uint sessionRef,
        [In] byte[] buffer,
        uint numberOfBytesToWrite,
        double timeout);

    // ── 属性设置 ────────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxSetProperty(
        uint sessionRef,
        uint propertyId,
        uint propertySize,
        ref uint value);

    [DllImport(DllName, EntryPoint = "nxSetProperty", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxSetPropertyUInt64(
        uint sessionRef,
        uint propertyId,
        uint propertySize,
        ref ulong value);

    [DllImport(DllName, EntryPoint = "nxSetProperty", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxSetPropertyByte(
        uint sessionRef,
        uint propertyId,
        uint propertySize,
        ref byte value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxGetProperty(
        uint sessionRef,
        uint propertyId,
        uint propertySize,
        out uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxSetPropertyString(
        uint sessionRef,
        uint propertyId,
        string value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxBlink(uint interfaceRef, uint modifier);

    // ── 状态读取 ────────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxReadState(
        uint sessionRef,
        uint stateId,
        uint stateSize,
        out uint stateValue,
        out int fault);

    // ── 状态码检查 ──────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nxStatusToString(
        int status,
        uint sizeOfString,
        [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder statusDescription);

    // ── 常量 ────────────────────────────────────────────────────

    // 会话模式（nixnet.h：FrameInStream=6，FrameOutStream=9）
    public const uint nxMode_FrameInStream = 6;
    public const uint nxMode_FrameOutStream = 9;

    // Frame Stream 模式无需数据库，必须使用特殊内存数据库名（空字符串会报 0xBFF63163）。
    // IO 模式（经典 / FD / FD+BRS）通过数据库名选择，Interface:CAN:I/O Mode 属性是只读的。
    public const string InMemoryDatabase = ":memory:";
    public const string InMemoryDatabaseCanFd = ":can_fd:";
    public const string InMemoryDatabaseCanFdBrs = ":can_fd_brs:";

    // 作用域
    public const uint nxScope_Normal = 0;

    // 属性 ID（nixnet.h：nxClass_Session=0x00100000 | 属性编号）
    public const uint nxPropSession_IntfBaudRate = 0x00100016;        // 仲裁段波特率 (U32)
    public const uint nxPropSession_IntfBaudRate64 = 0x09100016;      // 仲裁段波特率/自定义位时序 (U64)
    public const uint nxPropSession_QueueSize = 0x0010000C;          // 会话队列大小（字节，u32 --rw）
    public const uint nxPropSession_IntfCanFdBaudRate = 0x00100027;   // 数据段波特率 (U32)
    public const uint nxPropSession_IntfCanTerm = 0x00100025;         // 内置 120 Ω 终端电阻 (U32)
    public const uint nxPropSession_IntfCanIoMode = 0x00100026;       // IO 模式 (U32)
    public const uint nxPropSession_IntfEchoTx = 0x02100010;          // 发送完成回显 (Bool/U8)

    // CAN IO Mode 值
    public const uint nxCANioMode_CAN = 0;
    public const uint nxCANioMode_CAN_FD = 1;
    public const uint nxCANioMode_CAN_FD_BRS = 2;

    // 帧类型（Raw Frame 的 Type 字节，nixnet.h）
    public const byte nxFrameType_CAN_Data = 0x00;
    public const byte nxFrameType_CAN_Remote = 0x01;
    public const byte nxFrameType_CAN_BusError = 0x02;
    public const byte nxFrameType_CAN20_Data = 0x08;
    public const byte nxFrameType_CANFD_Data = 0x10;
    public const byte nxFrameType_CANFDBRS_Data = 0x18;

    // 扩展帧标志位于 Identifier 字段的 bit 29
    public const uint nxFrameId_CAN_IsExtended = 0x20000000;

    // Raw Frame Flags
    public const byte nxFrameFlags_TransmitEcho = 0x80;

    // 状态 ID（Interface Class | CAN Comm）
    public const uint nxState_CANComm = 0x00130010;

    // 读帧超时错误码（nxErrEventTimeout，0xBFF6300A）
    public const int nxErrEventTimeout = unchecked((int)0xBFF6300A);

    /// <summary>检查 NI-XNET 返回状态码：负数为错误抛出异常，正数为警告忽略</summary>
    public static void CheckStatus(int status)
    {
        if (status >= 0) return;
        var sb = new System.Text.StringBuilder(2048);
        nxStatusToString(status, 2048, sb);
        throw new InvalidOperationException($"NI-XNET 错误 ({status}): {sb}");
    }
}
