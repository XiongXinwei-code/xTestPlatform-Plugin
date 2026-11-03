using System.Runtime.InteropServices;

namespace CAN.Adapters.Zlg;

internal static partial class ZlgApi
{
    // These structs precede pack(push, 1) in the SDK header: use natural alignment.
    [StructLayout(LayoutKind.Sequential)]
    internal struct CloudChannel { public byte Enable, Type, IsUpload, IsDownload; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct CloudDevice
    {
        public int DeviceIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Type;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Owner;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Model;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string Firmware;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string Hardware;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Serial;
        public int Status;
        public byte GpsUpload, ChannelCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public CloudChannel[] Channels;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct CloudUserData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string UserName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Mobile;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string DllVersion;
        public UIntPtr DeviceCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)] public CloudDevice[] Devices;
    }
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "ZCLOUD_SetServerInfo")]
    internal static extern void CloudSetServerInfo(string httpServer, ushort httpPort, string authServer, ushort authPort);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "ZCLOUD_ConnectServer")]
    internal static extern uint CloudConnect(string userName, string password);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "ZCLOUD_IsConnected")]
    internal static extern uint CloudIsConnected();
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "ZCLOUD_DisconnectServer")]
    internal static extern uint CloudDisconnect();
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "ZCLOUD_GetUserData")]
    private static extern IntPtr CloudGetUserData(int update);

    internal static void ValidateCloudChannel(uint deviceIndex, uint channelIndex, bool fd)
    {
        var pointer = CloudGetUserData(1);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("ZLG Cloud 未返回设备目录");
        var data = Marshal.PtrToStructure<CloudUserData>(pointer);
        if (data.DeviceCount.ToUInt64() > 100) throw new InvalidOperationException("ZLG Cloud 设备数量超过当前 SDK 结构容量");
        var device = data.Devices.Take((int)data.DeviceCount.ToUInt64()).FirstOrDefault(d => d.DeviceIndex == deviceIndex);
        if (device.Channels is null) throw new InvalidOperationException($"ZLG Cloud 目录中不存在 SDK devIndex={deviceIndex}，设备索引不是目录行号");
        if (device.Status != 0 || device.ChannelCount > 16 || channelIndex >= device.ChannelCount)
            throw new InvalidOperationException("ZLG Cloud 设备离线或通道索引无效");
        var channel = device.Channels[channelIndex];
        if (channel.Enable != 1 || channel.IsUpload != 1 || channel.IsDownload != 1)
            throw new InvalidOperationException("ZLG Cloud 通道未启用双向上传/下载，请先在云设备配置中启用");
        if (fd && channel.Type == 0) throw new InvalidOperationException("ZLG Cloud 当前通道配置为 Classic，不能按 CAN FD 打开");
    }
}
