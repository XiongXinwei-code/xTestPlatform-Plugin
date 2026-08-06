using System.Runtime.InteropServices;

namespace LIN.Adapters.NiXnet;

/// <summary>NI-XNET LIN P/Invoke 接口（复用 nixnet.dll）</summary>
internal static class NiXnetLinApi
{
    private const string DllName = "nixnet.dll";

    // 会话模式常量
    internal const uint nxMode_FrameInStream  = 0;
    internal const uint nxMode_FrameOutStream = 2;

    // 属性 ID 常量
    internal const uint nxPropSession_IntfBaudRate = 0x03190009u;

    // 启停常量
    internal const uint nxStartStop_SessionOnly = 0;

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    internal static extern int nxCreateSession(
        string databaseName, string clusterName, string list,
        string interfaceName, uint mode, out uint sessionRef);

    [DllImport(DllName)]
    internal static extern int nxStart(uint sessionRef, uint scope);

    [DllImport(DllName)]
    internal static extern int nxStop(uint sessionRef, uint scope);

    [DllImport(DllName)]
    internal static extern int nxClear(uint sessionRef);

    [DllImport(DllName)]
    internal static extern int nxSetProperty(uint sessionRef, uint propertyId, uint propertySize, ref uint propertyValue);

    [DllImport(DllName)]
    internal static extern int nxWriteFrame(uint sessionRef, byte[] buffer, uint bufferSize, double timeout, out uint numberBytesWritten);

    [DllImport(DllName)]
    internal static extern int nxReadFrame(uint sessionRef, byte[] buffer, uint bufferSize, double timeout, out uint numberBytesRead);

    internal static void CheckStatus(int status)
    {
        if (status != 0)
            throw new InvalidOperationException($"NI-XNET LIN 操作失败，错误码: 0x{status:X8}");
    }
}
