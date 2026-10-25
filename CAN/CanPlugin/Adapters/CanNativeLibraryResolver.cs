using System.Reflection;
using System.Runtime.InteropServices;

namespace CAN.Adapters;

/// <summary>一个插件程序集只注册一个解析器，按库名分派到各厂商的部署目录。</summary>
internal static class CanNativeLibraryResolver
{
    static CanNativeLibraryResolver() =>
        NativeLibrary.SetDllImportResolver(typeof(CanNativeLibraryResolver).Assembly, Resolve);

    internal static void EnsureRegistered() { }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (string.Equals(libraryName, "zlgcan.dll", StringComparison.OrdinalIgnoreCase))
            return Zlg.ZlgApi.ResolveNativeLibrary(libraryName, assembly, searchPath);
        if (string.Equals(libraryName, "vxlapi64.dll", StringComparison.OrdinalIgnoreCase))
            return Vector.VectorXlApi.ResolveNativeLibrary(libraryName, assembly, searchPath);
        return IntPtr.Zero;
    }

    internal static IntPtr LoadBundled(string absolutePath, Assembly assembly) =>
        NativeLibrary.Load(absolutePath, assembly,
            DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
}
