using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using CAN.Adapters.Peak;
using CAN.Flash.HexParser;
using CAN.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class FullReadReauditTests
{
    [Theory]
    [InlineData(1, 0x51)] [InlineData(8, 0x58)]
    [InlineData(9, 0x509)] [InlineData(10, 0x50A)] [InlineData(11, 0x50B)]
    [InlineData(12, 0x50C)] [InlineData(13, 0x50D)] [InlineData(14, 0x50E)]
    [InlineData(15, 0x50F)] [InlineData(16, 0x510)]
    public void PeakUsbHandlesFollowOfficialNonContiguousConstants(int index, int expected)
    {
        Assert.Equal((ushort)expected, PcanApi.ParseChannel($"PCAN_USBBUS{index}"));
        Assert.Equal((ushort)expected, PcanApi.ParseChannel(index.ToString()));
    }

    [Fact]
    public async Task OpenEditorRejectsInvalidChannelExpression()
    {
        var errors = await new CAN.UI.CanOpenEditorPlugin().ValidateWithContextAsync(
            Round2XcpContractTests.Context(new CanOpenPlugin(), new CanOpenSetting { Channel = "invalid ( expression" }));
        Assert.Contains(errors, e => e.Code == "CAN_CHANNEL_EXPR");
    }

    [Fact]
    public void OverlapUsesFileOrderEvenWhenLaterRecordStartsEarlier()
    {
        var records = new List<(uint Address, byte[] Data)> { (0x101, [0xBB]), (0x100, [0xAA, 0xCC]) };
        var merged = Assert.Single(SegmentBuilder.Merge(records));
        Assert.Equal(0x100u, merged.StartAddress); Assert.Equal(new byte[] { 0xAA, 0xCC }, merged.Data);
        Assert.Equal(0x101u, records[0].Address); // Caller record order is not mutated.
    }

    [Fact]
    public void EqualAddressDuplicatesPreserveLastRecordAndEarlierUncoveredTail()
    {
        var records = new List<(uint Address, byte[] Data)> { (0x100, [1, 2, 3]) };
        for (int i = 0; i < 40; i++) records.Add((0x100, [(byte)i]));
        Assert.Equal(new byte[] { 39, 2, 3 }, Assert.Single(SegmentBuilder.Merge(records)).Data);
    }

    [Fact]
    public void MergePreservesHolesAdjacencyAndFinalAddress()
    {
        var segments = SegmentBuilder.Merge([(5, [5]), (1, [1]), (2, [2]), (uint.MaxValue, [0xFF])]);
        Assert.Equal(new uint[] { 1, 5, uint.MaxValue }, segments.Select(s => s.StartAddress));
        Assert.Equal(new byte[] { 1, 2 }, segments[0].Data);
        Assert.Throws<InvalidDataException>(() => SegmentBuilder.Merge([(uint.MaxValue, [1, 2])]));
    }

    [Fact]
    public void IntelHexParserRetainsLaterOverlappingRecordBytes()
    {
        var content = Encoding.ASCII.GetBytes(":01010100BB42\n:02010000AACC87\n:00000001FF\n");
        var segment = Assert.Single(new IntelHexParser().Parse(content, 0));
        Assert.Equal(0x100u, segment.StartAddress);
        Assert.Equal(new byte[] { 0xAA, 0xCC }, segment.Data);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void BothNativeVendorsRetainBundledPathsRegardlessOfInitializationOrder(bool zlgFirst)
    {
        // An isolated assembly owns an independent resolver registration. Empty files
        // exercise discovery only; no vendor DLL or device function is invoked.
        string directory = Path.Combine(Path.GetTempPath(), "can-resolver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "Native", "Zlg"));
        Directory.CreateDirectory(Path.Combine(directory, "Native", "Vector"));
        File.WriteAllBytes(Path.Combine(directory, "Native", "Zlg", "zlgcan.dll"), []);
        File.WriteAllBytes(Path.Combine(directory, "Native", "Vector", "vxlapi64.dll"), []);
        string assemblyPath = Path.Combine(directory, "CAN.StepPlugin.dll");
        File.Copy(typeof(PcanApi).Assembly.Location, assemblyPath);
        var load = new AssemblyLoadContext(Guid.NewGuid().ToString(), isCollectible: true);
        var oldSearch = new StringBuilder(32768); GetDllDirectory((uint)oldSearch.Capacity, oldSearch);
        try
        {
            var assembly = load.LoadFromAssemblyPath(assemblyPath);
            string[] vendors = zlgFirst ? ["Zlg.ZlgApi", "Vector.VectorXlApi"] : ["Vector.VectorXlApi", "Zlg.ZlgApi"];
            foreach (string vendor in vendors)
            {
                var type = assembly.GetType("CAN.Adapters." + vendor, throwOnError: true)!;
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                var nativeDir = (string?)type.GetField("NativeDir", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
                Assert.Equal(Path.Combine(directory, "Native", vendor.Split('.')[0]), nativeDir);
            }
            var currentSearch = new StringBuilder(32768); GetDllDirectory((uint)currentSearch.Capacity, currentSearch);
            Assert.Equal(oldSearch.ToString(), currentSearch.ToString());
            var resolver = assembly.GetType("CAN.Adapters.CanNativeLibraryResolver", true)!;
            var resolve = resolver.GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static)!;
            foreach (var vendor in new[] { (Type: "Zlg.ZlgApi", Dll: "zlgcan.dll"), (Type: "Vector.VectorXlApi", Dll: "vxlapi64.dll") })
            {
                Assert.Equal(IntPtr.Zero, resolve.Invoke(null, [vendor.Dll, assembly, null]));
                var failure = (string?)assembly.GetType("CAN.Adapters." + vendor.Type)!.GetField("LoadFailure", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
                Assert.False(string.IsNullOrWhiteSpace(failure)); // Empty bundled file is routed and its failure retained.
            }
            Assert.Equal(IntPtr.Zero, resolve.Invoke(null, ["unrelated-library.dll", assembly, null]));
        }
        finally
        {
            // The RED implementation modifies this process setting. Restore it so
            // the regression itself cannot affect unrelated native loader tests.
            var finalSearch = new StringBuilder(32768); GetDllDirectory((uint)finalSearch.Capacity, finalSearch);
            if (oldSearch.ToString() != finalSearch.ToString())
                SetDllDirectory(oldSearch.Length == 0 ? null : oldSearch.ToString());
            load.Unload();
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetDllDirectory(uint count, StringBuilder path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string? path);
}
