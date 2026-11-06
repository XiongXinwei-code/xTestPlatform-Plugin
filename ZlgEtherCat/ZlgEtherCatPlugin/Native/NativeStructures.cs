using System.Runtime.InteropServices;

namespace ZlgEtherCat.Native;

internal static class NativeStructures
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DriverInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] DriverVersion;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] ApiVersion;
        public uint BoardCount;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct BoardInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Version;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SerialNumber;
        public uint Alias, ChannelCount, FpgaVersion;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] Reserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct EcatInfo
    {
        public uint QueueDepth;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] Reserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct EniInfo { public uint MainCycle, MailboxCycle, OutputBytes, InputBytes; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct SlaveInfo
    {
        public uint VendorId, ProductCode, SerialNumber, Revision, DeviceType, Alias, OutputBits, InputBits;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] Reserved;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SlaveIdentity { public uint VendorId, ProductCode, Revision, SerialNumber; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SlaveDiagnosticInfo
    {
        public uint State;
        public SlaveIdentity Identity;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] PortCounters;
        public byte ProcessingUnitErrors, PdiErrors;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] Reserved;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct MasterDiagnosticInfo
    {
        public uint State;
        public ushort NetworkState, MainSlaveCount, RedundancySlaveCount, ValidLicense;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] ExpirationDate;
        public int ExpirationLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] LicensedTo;
        public int LicensedToLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] Reserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct TopologyNode
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)] public byte[] Name;
        public uint VendorId, ProductCode;
        public ushort Port, PhysicalPosition, IncrementAddress, FixedAddress, Alias, RequestingId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public ushort[] Reserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Topology
    {
        public byte Status;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public byte[] Reserved;
        public uint Count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)] public TopologyNode[] Nodes;
    }
}
