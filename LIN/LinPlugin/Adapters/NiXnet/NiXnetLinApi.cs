using System.Runtime.InteropServices;

namespace LIN.Adapters.NiXnet;

/// <summary>NI-XNET LIN P/Invoke 接口（复用 nixnet.dll，定义与 nixnet.h 对齐）</summary>
internal static class NiXnetLinApi
{
    private const string DllName = "nixnet.dll";

    // ── 会话管理 ──────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nxCreateSession(
        string databaseName,
        string clusterName,
        string list,
        string interfaceName,
        uint mode,
        out uint sessionRef);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nxClear(uint sessionRef);

    // ── 启动/停止 ─────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nxStart(uint sessionRef, uint scope);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nxStop(uint sessionRef, uint scope);

    // ── 读写帧 ────────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nxReadFrame(
        uint sessionRef,
        byte[] buffer,
        uint sizeOfBuffer,
        double timeout,
        out uint numberOfBytesReturned);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nxWriteFrame(
        uint sessionRef,
        byte[] buffer,
        uint numberOfBytesToWrite,
        double timeout);

    // ── 属性设置 ──────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nxSetProperty(
        uint sessionRef,
        uint propertyId,
        uint propertySize,
        ref uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nxSetProperty(
        uint sessionRef,
        uint propertyId,
        uint propertySize,
        ref byte value);

    // ── 状态码检查 ────────────────────────────────────────────
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void nxStatusToString(
        int status,
        uint sizeOfString,
        [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder statusDescription);

    // ── 常量 ──────────────────────────────────────────────────

    // 会话模式（nixnet.h：FrameInStream=6，FrameOutStream=9）
    internal const uint nxMode_FrameInStream = 6;
    internal const uint nxMode_FrameOutStream = 9;

    // Frame Stream 模式无需数据库，必须使用特殊内存数据库名（空字符串会报 0xBFF63163）
    internal const string InMemoryDatabase = ":memory:";

    // 作用域
    internal const uint nxScope_Normal = 0;

    // 属性 ID（取自 NI-XNET 驱动 nixnet.h，高位编码了数据类型）
    internal const uint nxPropSession_IntfBaudRate = 0x00100016;   // 波特率 (u32 --rw)
    internal const uint nxPropSession_QueueSize = 0x0010000C;      // 会话队列大小（字节，u32 --rw）
    internal const uint nxPropSession_IntfLINMaster = 0x02100072;  // LIN 主节点 (bool --rw，按 u32 传递)
    internal const uint nxPropSession_IntfLINSleep = 0x00100073;   // LIN 睡眠/唤醒状态 (u32 --w)

    // nxPropSession_IntfLINSleep 取值（nixnet.h）
    internal const uint nxLINSleep_RemoteSleep = 0; // 总线睡眠（发送 Go-to-Sleep 命令）
    internal const uint nxLINSleep_RemoteWake = 1;  // 总线唤醒（发送唤醒模式）
    internal const uint nxLINSleep_LocalSleep = 2;  // 仅本地接口睡眠
    internal const uint nxLINSleep_LocalWake = 3;   // 仅本地接口唤醒

    // LIN 帧类型（Raw Frame 的 Type 字节，nixnet.h）
    internal const byte nxFrameType_LIN_Data = 0x40;
    internal const byte nxFrameType_LIN_BusError = 0x41;
    internal const byte nxFrameType_LIN_NoResponse = 0x42;

    // 读帧超时错误码（nxErrEventTimeout）
    internal const int nxErrEventTimeout = unchecked((int)0xBFF6300A);

    /// <summary>检查 NI-XNET 返回状态码：负数为错误抛出异常，正数为警告忽略</summary>
    internal static void CheckStatus(int status, string context = "")
    {
        if (status >= 0) return;
        var sb = new System.Text.StringBuilder(2048);
        nxStatusToString(status, 2048, sb);
        var prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
        throw new InvalidOperationException($"NI-XNET LIN 错误 {prefix}({status}): {sb}");
    }
}
