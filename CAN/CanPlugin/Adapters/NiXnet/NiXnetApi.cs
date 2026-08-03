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
        byte[] buffer,
        uint sizeOfBuffer,
        double timeout,
        out uint numberOfBytesReturned);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxWriteFrame(
        uint sessionRef,
        byte[] buffer,
        uint numberOfBytesToWrite,
        double timeout);

    // ── 属性设置 ────────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nxSetProperty(
        uint sessionRef,
        uint propertyId,
        uint propertySize,
        ref uint value);

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

    // ── 状态码检查 ──────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nxStatusToString(
        int status,
        uint sizeOfString,
        [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder statusDescription);

    // ── 常量 ────────────────────────────────────────────────────

    // 会话模式
    public const uint nxMode_FrameInStream = 2;
    public const uint nxMode_FrameOutStream = 3;

    // 作用域
    public const uint nxScope_Normal = 0;

    // 属性 ID
    public const uint nxPropSession_IntfBaudRate = 0x00000064;        // 仲裁段波特率
    public const uint nxPropSession_IntfCanFdBaudRate = 0x00000065;   // 数据段波特率
    public const uint nxPropSession_IntfCanIoMode = 0x00000066;       // IO 模式
    public const uint nxPropSession_IntfCanTransceiverType = 0x00000067;

    // CAN IO Mode 值
    public const uint nxCANioMode_CAN = 0;
    public const uint nxCANioMode_CAN_FD = 1;
    public const uint nxCANioMode_CAN_FD_BRS = 2;
    public const uint nxCANioMode_CAN_XL = 3;

    // 帧结构中的标志位
    public const byte nxFrameType_CAN_Data = 0x00;
    public const byte nxFrameType_CAN_20 = 0x00;
    public const byte nxFrameType_CAN_FD = 0x10;
    public const byte nxFrameType_CAN_BRS = 0x20;

    /// <summary>检查 NI-XNET 返回状态码，非 0 则抛出异常</summary>
    public static void CheckStatus(int status)
    {
        if (status == 0) return;
        var sb = new System.Text.StringBuilder(2048);
        nxStatusToString(status, 2048, sb);
        throw new InvalidOperationException($"NI-XNET 错误 ({status}): {sb}");
    }
}
