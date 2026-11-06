using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LabVIEWCallPlugin.Execution;

internal static class ViServerProbe
{
    private const uint ErrorInsufficientBuffer = 122;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint AfInet = 2;
    private const int OwnerPidListener = 3;
    private const int MaxTableBytes = 16 * 1024 * 1024;

    internal static async Task<ViServerIdentity> CheckAsync(int port, CancellationToken ct)
    {
        var identity = CaptureIdentity(port, ct);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(IPAddress.Loopback, port, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
        catch (SocketException error)
        {
            throw ConnectionError(port, error);
        }
        VerifyUnchanged(identity, ct);
        return identity;
    }

    // These queries do not send a VI request or activate, stop, or reconfigure LabVIEW.
    internal static ViServerIdentity CaptureIdentity(int port, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);
        ct.ThrowIfCancellationRequested();
        int owner = SelectListenerOwner(ReadListeners(ct), port);
        ct.ThrowIfCancellationRequested();
        try
        {
            using var process = OpenProcess(ProcessQueryLimitedInformation, false, owner);
            if (process.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var path = new StringBuilder(32768);
            int size = path.Capacity;
            if (!QueryFullProcessImageName(process, 0, path, ref size))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!GetProcessTimes(process, out long created, out _, out _, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!IsWow64Process2(process, out ushort processMachine, out ushort nativeMachine))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            string imagePath = Path.GetFullPath(path.ToString());
            string version = FileVersionInfo.GetVersionInfo(imagePath).FileVersion ?? "未知";
            var identity = new ViServerIdentity(port, owner, DateTime.FromFileTimeUtc(created), imagePath,
                version, processMachine == 0 ? nativeMachine : processMachine);
            identity.Validate();
            ct.ThrowIfCancellationRequested();
            // Re-read the owner after opening the process to reject an endpoint that changed hands.
            if (SelectListenerOwner(ReadListeners(ct), port) != owner)
                throw new InvalidOperationException($"VI Server 端口所有者在检查期间发生变化；{identity.Diagnostic}。本次身份检查未通过。");
            return identity;
        }
        catch (Exception error) when (error is Win32Exception or ArgumentException or IOException or EntryPointNotFoundException)
        {
            throw new InvalidOperationException($"无法确认 VI Server 端口 {port} 的进程身份（PID={owner}），本次身份检查未通过：{error.Message}", error);
        }
    }

    internal static void VerifyUnchanged(ViServerIdentity expected, CancellationToken ct)
        => expected.EnsureSame(CaptureIdentity(expected.Port, ct));

    internal static int SelectListenerOwner(IEnumerable<ViServerListener> rows, int port)
    {
        var owners = rows.Where(row => row.State == 2 && row.Port == port &&
                (row.Address.Equals(IPAddress.Any) || row.Address.Equals(IPAddress.Loopback)))
            .Select(row => row.ProcessId).Distinct().ToArray();
        if (owners.Length == 0)
            throw ConnectionError(port, new SocketException((int)SocketError.ConnectionRefused));
        if (owners.Length != 1 || owners[0] <= 0)
            throw new InvalidOperationException($"VI Server 端口 {port} 的进程所有者不唯一或无效，已拒绝连接：{string.Join(", ", owners)}。");
        return owners[0];
    }

    private static ViServerListener[] ReadListeners(CancellationToken ct)
    {
        uint bytes = 0;
        uint status = GetExtendedTcpTable(IntPtr.Zero, ref bytes, false, AfInet, OwnerPidListener, 0);
        if (status != ErrorInsufficientBuffer && status != 0) throw new Win32Exception((int)status);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (bytes < sizeof(uint) || bytes > MaxTableBytes)
                throw new InvalidDataException($"Windows TCP 所有者表大小异常：{bytes}。");
            int capacity = (int)bytes;
            IntPtr pointer = Marshal.AllocHGlobal(capacity);
            try
            {
                status = GetExtendedTcpTable(pointer, ref bytes, false, AfInet, OwnerPidListener, 0);
                if (status == ErrorInsufficientBuffer) continue;
                if (status != 0) throw new Win32Exception((int)status);
                if (bytes > capacity) throw new InvalidDataException("Windows TCP 所有者表超过已分配容量。");
                var table = new byte[bytes];
                Marshal.Copy(pointer, table, 0, table.Length);
                return DecodeOwnerTable(table);
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }
        throw new InvalidOperationException("Windows TCP 所有者表持续变化，无法确认 VI Server 身份，请重试。");
    }

    internal static ViServerListener[] DecodeOwnerTable(ReadOnlySpan<byte> table)
    {
        // MIB_TCPTABLE_OWNER_PID: DWORD count, followed by MIB_TCPROW_OWNER_PID.
        // Each IPv4 row contains six DWORDs (24 bytes); ports are in network byte order.
        // https://learn.microsoft.com/windows/win32/api/tcpmib/ns-tcpmib-mib_tcprow_owner_pid
        const int rowBytes = 24;
        if (table.Length < 4) throw new InvalidDataException("Windows TCP 所有者表缺少行数。");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(table);
        if (count > (table.Length - 4) / rowBytes)
            throw new InvalidDataException("Windows TCP 所有者表行数超出缓冲区。");
        var rows = new ViServerListener[count];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = table.Slice(4 + i * rowBytes, rowBytes);
            uint pid = BinaryPrimitives.ReadUInt32LittleEndian(row[20..]);
            if (pid > int.MaxValue) throw new InvalidDataException("Windows TCP 所有者 PID 超出有效范围。");
            rows[i] = new(BinaryPrimitives.ReadUInt32LittleEndian(row), new IPAddress(row.Slice(4, 4)),
                BinaryPrimitives.ReadUInt16BigEndian(row[8..]), (int)pid);
        }
        return rows;
    }

    private static InvalidOperationException ConnectionError(int port, SocketException error) => new(
        $"无法连接本机 VI Server（127.0.0.1:{port}，{error.SocketErrorCode}）。"
        + $"请启动目标 LabVIEW，在 Tools → Options → VI Server 启用 TCP/IP 并设置端口 {port}，允许本机 IPv4 访问。"
        + "仅安装 Runtime 不会自动启动 VI Server。", error);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref uint size, [MarshalAs(UnmanagedType.Bool)] bool order,
        uint addressFamily, int tableClass, uint reserved);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process2(SafeProcessHandle process, out ushort processMachine, out ushort nativeMachine);
}

internal readonly record struct ViServerListener(uint State, IPAddress Address, int Port, int ProcessId);
