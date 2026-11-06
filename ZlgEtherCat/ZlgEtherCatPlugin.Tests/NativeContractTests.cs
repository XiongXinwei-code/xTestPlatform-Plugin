using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;
using ZlgEtherCat.Native;

namespace ZlgEtherCat.Tests;

public sealed class NativeContractTests
{
    [Theory]
    [InlineData("DriverInfo", 68)]
    [InlineData("BoardInfo", 124)]
    [InlineData("EcatInfo", 20)]
    [InlineData("EniInfo", 16)]
    [InlineData("SlaveInfo", 56)]
    [InlineData("SlaveDiagnosticInfo", 40)]
    [InlineData("MasterDiagnosticInfo", 118)]
    public void VendorStructuresHaveDocumentedNativeSizes(string name, int size)
    {
        var type = typeof(ZlgNativeApi).Assembly.GetType("ZlgEtherCat.Native.NativeStructures+" + name, true)!;
        Assert.Equal(size, Marshal.SizeOf(type));
    }

    [Fact]
    public void InvalidInputsAreRejectedBeforeAnyNativeLibraryCanLoad()
    {
        var api = new ZlgNativeApi();
        Assert.Throws<ArgumentException>(() => api.Open("AL1\0ID2", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => api.ReadSdo(new IntPtr(1), 0, 0x6041, 0, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => api.ReadPi(new IntPtr(1), PiArea.LocalInput, uint.MaxValue, 2));
        Assert.Throws<ArgumentException>(() => api.MaskWritePi(new IntPtr(1), PiArea.LocalOutput, 0, new byte[1], new byte[2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => api.PopInput(new IntPtr(1), false, 0));
        Assert.Throws<ArgumentException>(() => api.WritePi(new IntPtr(1), PiArea.ShadowInput, 0, new byte[2]));
    }

    [Fact]
    public void ImportsUseCdeclAndExplicitOneByteNativeBools()
    {
        var imports = typeof(ZlgNativeApi).Assembly.GetType("ZlgEtherCat.Native.NativeMethods", true)!;
        var methods = imports.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<DllImportAttribute>() is not null).ToArray();
        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            Assert.Equal(CallingConvention.Cdecl, method.GetCustomAttribute<DllImportAttribute>()!.CallingConvention);
            foreach (var parameter in method.GetParameters().Where(p => p.ParameterType == typeof(bool)))
                Assert.Equal(UnmanagedType.I1, parameter.GetCustomAttribute<MarshalAsAttribute>()!.Value);
            if (method.ReturnType == typeof(bool))
                Assert.Equal(UnmanagedType.I1, method.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()!.Value);
        }
    }
}
