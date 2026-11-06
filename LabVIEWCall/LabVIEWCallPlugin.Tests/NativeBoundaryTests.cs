using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.LVadapter.Interop;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public class NativeBoundaryTests
{
    [Fact]
    public void PanelJsonUsesUtf8InsteadOfTheNativePathCodePage()
    {
        const string value = "{\"Name\":\"带有噪声的正弦波\",\"Variable\":\"Locals.结果\"}";
        var bytes = LvLibHelper.EncodeJsonCString(value, 256);
        Assert.Equal(value, LvLibHelper.ReadJsonCString(bytes));
        Assert.Throws<InvalidDataException>(() => LvLibHelper.EncodeJsonCString(value, value.Length + 2));
        Assert.Throws<System.Text.DecoderFallbackException>(() => LvLibHelper.ReadJsonCString([0xff, 0, 0]));
        Assert.Throws<ArgumentException>(() => LvLibHelper.EncodeJsonCString("a\0b", 64));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(92)]
    [InlineData(28808)]
    public void GeneratedPixmapDeallocatorReturnsReleasedBytes(int bytes)
        => LvLibHelper.ValidatePixmapRelease(bytes, IntPtr.Zero, bytes);

    [Theory]
    [InlineData(0, 0, 12)]
    [InlineData(12, 1, 12)]
    [InlineData(-1, 0, 12)]
    [InlineData(92, 0, 12)]
    public void PixmapReleaseMustClearTheHandleAndMatchItsAllocation(int returned, int handle, int bytes)
        => Assert.Throws<InvalidOperationException>(() => LvLibHelper.ValidatePixmapRelease(returned, (IntPtr)handle, bytes));

    [Theory]
    [InlineData("LVrte_LoadVI", new int[] { 2 })]
    [InlineData("LVrte_RunVI", new int[] { 0, 3 })]
    [InlineData("LVrte_SetParameters", new int[] { 0, 4 })]
    [InlineData("LVrte_GetParameters", new int[] { 0, 2 })]
    public void NativeViReferencesMatchSdkCookieWidth(string export, int[] indexes)
    {
        var entry = Assert.Single(typeof(LvLibWrapper).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.GetCustomAttribute<DllImportAttribute>()?.EntryPoint == export);
        foreach (int index in indexes)
            Assert.Equal(typeof(uint).MakeByRefType(), entry.GetParameters()[index].ParameterType);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x80000001u)]
    [InlineData(uint.MaxValue)]
    public void NativeViReferencePreservesAllUnsignedBits(uint cookie)
    {
        var managed = LvLibWrapper.FromNativeRefNum(cookie);
        Assert.Equal((long)cookie, managed.ToInt64());
        Assert.Equal(cookie, LvLibWrapper.ToNativeRefNum(managed));
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(0x100000001L)]
    public void OutOfRangeReferencesCannotReachTheNativeBridge(long value)
    {
        var reference = new IntPtr(value);
        Assert.False(LvLibWrapper.SafeRunVI(reference, false, false, out _, out _, out var runError));
        Assert.IsType<ArgumentException>(runError);
        Assert.False(LvLibWrapper.SafeSetParameter(reference, "x", [4, 3], [0], out _, out _, out var setError));
        Assert.IsType<ArgumentException>(setError);
        Assert.False(LvLibWrapper.SafeGetParameter(reference, "x", out _, out _, out _, out _, out var getError));
        Assert.IsType<ArgumentException>(getError);
    }

    [Fact]
    public void PackagedBridgeMatchesInspectedBinaryWithoutLoadingIt()
    {
        using var file = File.OpenRead(Path.Combine(Path.GetDirectoryName(typeof(LvLibHelper).Assembly.Location)!, "lvLib.dll"));
        Assert.Equal(LvBridgeLibrary.ExpectedHash, Convert.ToHexString(SHA256.HashData(file)));
    }

    [Theory]
    [InlineData(127, 65535)]
    [InlineData(128, 65536)]
    [InlineData(129, 65540)]
    public void VerifiedReadPreservesZerosAndFfAndGrowsOnlyReads(int words, int bytes)
    {
        var expectedType = Enumerable.Repeat((short)-1, words).ToArray();
        expectedType[0] = (short)(words * sizeof(short));
        var expectedData = new byte[bytes];
        expectedData[0] = 0x55;
        expectedData[^1] = 0xff;
        int calls = 0;
        IntPtr Get(short[] type, byte[] data)
        {
            ++calls;
            Array.Copy(expectedType, type, Math.Min(type.Length, words));
            Array.Copy(expectedData, data, Math.Min(data.Length, bytes));
            return (IntPtr)17;
        }
        var result = LvOutputReader.Read(Get, 128, 65536, CancellationToken.None, (IntPtr)17);
        Assert.Equal((IntPtr)17, result.Reference);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(expectedData, result.Data);
        Assert.Equal(words > 128 || bytes > 65536 ? 4 : 2, calls);
    }

    [Fact]
    public void DifferentOrMissingReadbackIsNeverDecoded()
    {
        byte next = 0;
        IntPtr Get(short[] words, byte[] data)
        {
            words[0] = 4;
            words[1] = 3;
            data[0] = ++next;
            return (IntPtr)1;
        }
        Assert.Throws<InvalidDataException>(() => LvOutputReader.Read(Get, 128, 64, default, (IntPtr)1));
        Assert.Throws<InvalidDataException>(() => LvOutputReader.Read((_, _) => (IntPtr)1, 128, 64, default, (IntPtr)1));
    }

    [Fact]
    public void TypeAboveVerifiedLimitIsRejectedWithoutAThirdRead()
    {
        int calls = 0;
        IntPtr Get(short[] words, byte[] data)
        {
            calls++;
            Array.Fill(words, (short)1);
            data[0] = 0;
            return (IntPtr)1;
        }
        Assert.Throws<InvalidDataException>(() =>
            LvOutputReader.Read(Get, LvOutputReader.MaxTypeWords, 64, default, (IntPtr)1));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void CancellationAfterOneReadPreventsAnotherSubmission()
    {
        using var source = new CancellationTokenSource();
        int calls = 0;
        IntPtr Get(short[] words, byte[] data)
        {
            ++calls;
            source.Cancel();
            return (IntPtr)1;
        }
        Assert.Throws<OperationCanceledException>(() => LvOutputReader.Read(Get, 128, 64, source.Token, (IntPtr)1));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(65536, 65536)]
    [InlineData(-2, 4)]
    [InlineData(4097, 4096)]
    public void InvalidPixmapDimensionsRejectedBeforeMarshalCopy(int height, int width)
        => Assert.Throws<InvalidDataException>(() => LvLibHelper.GetPixmapPixelCount(height, width));

    [Fact]
    public void MetadataMustHaveAnUnambiguousTerminatorInsideItsByteCapacity()
    {
        Assert.Equal("ok", LvLibHelper.ReadCString([111, 107, 0, 0]));
        Assert.Throws<InvalidDataException>(() => LvLibHelper.ReadCString([111, 107, 0]));
        Assert.Throws<InvalidDataException>(() => LvLibHelper.ReadCString([111, 107]));
    }

    [Fact]
    public void NativeIdentifiersMustRoundTripThroughTheActiveAnsiCodePage()
    {
        var western = System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        LvAnsiText.Check(@"C:\Tests\café.vi", "path", western);
        Assert.Throws<ArgumentException>(() => LvAnsiText.Check(@"C:\测试.vi", "path", western));
        Assert.Throws<ArgumentException>(() => LvAnsiText.Check("Status\0Other", "terminal", western));
    }

    [Fact]
    public void InvalidNativeIdentifiersFailBeforeAnyDllCall()
    {
        const string invalid = "ok\0other";
        Assert.False(LvLibWrapper.SafeLoadVI(3363, invalid, out _, out _, out var load));
        Assert.IsType<ArgumentException>(load);
        Assert.False(LvLibWrapper.SafeSetParameter((IntPtr)1, invalid, [4, 3], [0], out _, out _, out var set));
        Assert.IsType<ArgumentException>(set);
        Assert.False(LvLibWrapper.SafeGetParameter((IntPtr)1, invalid, out _, out _, out _, out _, out var get));
        Assert.IsType<ArgumentException>(get);
        Assert.Throws<ArgumentException>(() => LvLibHelper.EditVI(invalid));
        Assert.Throws<ArgumentException>(() => LvLibHelper.GetLibItems(invalid));
        Assert.Throws<ArgumentException>(() => LvLibHelper.GetConnectPanel(invalid));
    }

    [Fact]
    public void EmptyReferenceCannotReachReadOrWrite()
    {
        Assert.False(LvLibWrapper.SafeSetParameter(IntPtr.Zero, "Input", [4, 3], [0], out _, out _, out var set));
        Assert.IsType<ArgumentException>(set);
        Assert.False(LvLibWrapper.SafeGetParameter(IntPtr.Zero, "Output", out _, out _, out _, out _, out var get));
        Assert.IsType<ArgumentException>(get);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    public void EmptyOrDifferentReturnedReferenceStopsBeforeASecondRead(int returned)
    {
        int reads = 0;
        Assert.Throws<InvalidDataException>(() => LvOutputReader.Read((type, data) =>
        {
            ++reads;
            return (IntPtr)returned;
        }, 16, 32, default, (IntPtr)17));
        Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RetirementBeforeEachProbeOrGrowthReadPreventsNativeSubmission(int blockedAt)
    {
        int checks = 0, reads = 0;
        void CheckActive()
        {
            if (++checks == blockedAt) throw new InvalidOperationException("执行代次已回收");
        }
        Assert.Throws<InvalidOperationException>(() => LvOutputReader.Read((type, data) =>
        {
            ++reads;
            type[0] = 4;
            type[1] = 3;
            data.AsSpan(0, Math.Min(8, data.Length)).Fill(0xcc);
            return (IntPtr)17;
        }, 16, 4, default, (IntPtr)17, CheckActive));
        Assert.Equal(blockedAt - 1, reads);
    }

    [Theory]
    [InlineData("FFFFFFFF")]
    [InlineData("40000000")]
    [InlineData("0000000200000000")]
    public void InvalidLengthsAreRejectedBeforeAllocatingNativeDecodedArrays(string hex)
    {
        byte[] payload = Convert.FromHexString(hex);
        Assert.Throws<InvalidDataException>(() => LvFlattenHelper.DecodeInt32Array(payload));
        Assert.Throws<InvalidDataException>(() => LvFlattenHelper.DecodeFloat64Array(payload));
        Assert.Throws<InvalidDataException>(() => LvFlattenHelper.DecodeStringArray(payload));
    }
}
