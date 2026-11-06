using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using ZlgEtherCat.Native;

namespace ZlgEtherCat.Tests;

public sealed class NativeVendorTests
{
    [Fact]
    public void BundledX64BinaryIsOriginalAndContainsAllDeclaredAndUsedExports()
    {
        var vendor = VendorDirectory();
        var bytes = File.ReadAllBytes(Path.Combine(vendor, "x64", "pci_zecm.dll"));
        Assert.Equal("C294DC4B9FD3CE1D31357B7DF950633DE361A0FBB7B2621F2DD1836304C415DF", Convert.ToHexString(SHA256.HashData(bytes)));
        var pe = I32(bytes, 60);
        Assert.Equal(0x8664, U16(bytes, pe + 4));
        var exports = ExportNames(bytes);
        var headers = string.Join("\n", new[] { "pci_zecm.h", "pci_diagnostic.h", "pci_errno.h" }.Select(name => File.ReadAllText(Path.Combine(vendor, "include", name))));
        var declarations = Regex.Matches(headers, @"(?m)^EXPORT\s+[^;()]*?\b(?<name>[A-Za-z_]\w*)\s*\(")
            .Select(m => m.Groups["name"].Value).Distinct().ToArray();
        Assert.Equal(147, declarations.Length);
        Assert.Equal(151, exports.Count);
        Assert.All(declarations, name => Assert.Contains(name, exports));
        var methods = typeof(ZlgNativeApi).Assembly.GetType("ZlgEtherCat.Native.NativeMethods", true)!.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var method in methods)
        {
            var import = method.GetCustomAttribute<DllImportAttribute>();
            if (import is null) continue;
            Assert.Contains(import.EntryPoint ?? method.Name, exports);
            Assert.DoesNotContain(method.Name, new[] { "EcatRMEnable", "EcatRMEnableSet", "EcatRMEnableGet", "EcatPIGetRange", "EcatPIWatchDogStart", "EcatPIWatchDogStop" });
        }
    }

    [Fact]
    public void BufferTrimmingAndQueueTimeoutDoNotInventSuccessfulSamples()
    {
        Assert.False(ZlgNativeApi.QueueResult(2, "EcatPIInputQueuePop"));
        Assert.False(ZlgNativeApi.QueueResult(5, "EcatPIInputQueuePop"));
        Assert.False(ZlgNativeApi.QueueResult(32881, "EcatPIInputQueuePop"));
        Assert.True(ZlgNativeApi.QueueResult(0, "EcatPIInputQueuePop"));
        Assert.Equal(new byte[] { 1, 2 }, ZlgNativeApi.Actual([1, 2, 3, 4], 2));
        Assert.Throws<InvalidOperationException>(() => ZlgNativeApi.Actual(new byte[1], 2));
    }

    [Fact]
    public void PackedDiagnosticsAndTopologyUseVendorFieldOffsets()
    {
        var assembly = typeof(ZlgNativeApi).Assembly;
        var slave = assembly.GetType("ZlgEtherCat.Native.NativeStructures+SlaveDiagnosticInfo", true)!;
        Assert.Equal(20, Marshal.OffsetOf(slave, "PortCounters").ToInt32());
        Assert.Equal(36, Marshal.OffsetOf(slave, "ProcessingUnitErrors").ToInt32());
        var master = assembly.GetType("ZlgEtherCat.Native.NativeStructures+MasterDiagnosticInfo", true)!;
        Assert.Equal(44, Marshal.OffsetOf(master, "ExpirationLength").ToInt32());
        Assert.Equal(112, Marshal.OffsetOf(master, "LicensedToLength").ToInt32());
        Assert.Equal(48, Marshal.SizeOf(assembly.GetType("ZlgEtherCat.Native.NativeStructures+TopologyNode", true)!));
        Assert.Equal(488, Marshal.SizeOf(assembly.GetType("ZlgEtherCat.Native.NativeStructures+Topology", true)!));
    }

    [Fact]
    public void AdvancedOutputBoundsFailBeforeLoadingTheDeviceLibrary()
    {
        var api = new ZlgNativeApi(); var handle = new IntPtr(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => api.WriteBoardDo(handle, 0x10000));
        Assert.Throws<ArgumentOutOfRangeException>(() => api.OutputPwm(handle, 4, 1000, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => api.ConfigureLatch(handle, 0, 5, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => api.ReadSdo(handle, 0, 0x1000, 2, 4, 100, true));
        Assert.Throws<ArgumentException>(() => api.ConfigureForcedOutput(handle, 0, 2, 0, new byte[3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => api.ReadSlaveMemory(handle, 0, 65535, 2, false));
    }

    private static string VendorDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "ZlgEtherCat", "ZlgEtherCatPlugin", "Native", "vendor");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("找不到 SDK 来源校验目录。");
    }
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static int I32(byte[] bytes, int offset) => checked((int)U32(bytes, offset));
    private static HashSet<string> ExportNames(byte[] bytes)
    {
        var pe = I32(bytes, 60); var opt = pe + 24;
        var sectionStart = opt + U16(bytes, pe + 20);
        int Offset(uint rva)
        {
            for (var i = 0; i < U16(bytes, pe + 6); i++)
            {
                var s = sectionStart + i * 40; var va = U32(bytes, s + 12);
                var size = Math.Max(U32(bytes, s + 8), U32(bytes, s + 16));
                if (rva >= va && rva - va < size) return checked((int)(U32(bytes, s + 20) + rva - va));
            }
            throw new InvalidDataException("PE 中的 RVA 无对应节。");
        }
        var export = Offset(U32(bytes, opt + 112));
        var count = U32(bytes, export + 24); var names = Offset(U32(bytes, export + 32));
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var start = Offset(U32(bytes, names + i * 4)); var end = Array.IndexOf(bytes, (byte)0, start);
            if (end < start) throw new InvalidDataException("导出名称未结束。");
            result.Add(Encoding.ASCII.GetString(bytes, start, end - start));
        }
        return result;
    }
}
