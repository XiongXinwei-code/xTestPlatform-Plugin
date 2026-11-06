using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LabVIEWCallPlugin.LVadapter;

/// <summary>只加载随当前插件部署、已核对读写 ABI 的 x64 桥。</summary>
internal static class LvBridgeLibrary
{
    internal const string ExpectedHash = "D1692E13EA92D4D19768B4F6AE73B01D6BE1985A9472DB7BB58784BDF3A0C9EF";
    private static readonly object Gate = new();
    private static ModuleHandle? module;
    private static readonly List<ModuleHandle> SystemRuntimeModules = [];

    internal static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? search)
    {
        if (!string.Equals(name, "lvLib.dll", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;
        if (Assembly.GetEntryAssembly()?.GetName().Name != "LabVIEWCall.BridgeHost")
            throw new InvalidOperationException("禁止在平台进程内加载 LabVIEW 原生桥，请使用独立桥接进程。");
        lock (Gate)
        {
            if (module is null)
            {
                // A host may already have loaded a third-party private MSVCP140. In this
                // clean process use the system redistributable before NI initializes ICU.
                PrepareSystemRuntime(assembly);
                var folder = string.IsNullOrEmpty(assembly.Location)
                    ? Path.Combine(AppContext.BaseDirectory, "Plugins", "LabVIEWCall")
                    : Path.GetDirectoryName(assembly.Location)!;
                var path = Path.Combine(folder, "lvLib.dll");
                // 同一文件保持只读共享直到加载完成，避免校验和加载命中不同文件。
                using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (Convert.ToHexString(SHA256.HashData(file)) != ExpectedHash)
                    throw new BadImageFormatException($"LabVIEW 桥接库与已验证版本不匹配，请重新部署完整插件目录：{path}");
                var handle = NativeLibrary.Load(path, assembly,
                    DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
                module = new ModuleHandle(handle);
            }
            return module.DangerousGetHandle();
        }
    }

    private static void PrepareSystemRuntime(Assembly assembly)
    {
        if (SystemRuntimeModules.Count != 0) return;
        VerifyLoadedRuntimePaths();
        var loaded = new List<ModuleHandle>();
        try
        {
            foreach (var runtime in new[] { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll" })
            {
                var path = Path.Combine(Environment.SystemDirectory, runtime);
                var native = new ModuleHandle(NativeLibrary.Load(path, assembly, DllImportSearchPath.System32));
                loaded.Add(native);
                var actual = new StringBuilder(32768);
                if (GetModuleFileName(native.DangerousGetHandle(), actual, actual.Capacity) == 0 ||
                    !string.Equals(Path.GetFullPath(actual.ToString()), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"辅助进程加载了非预期 C++ 运行库：{actual}；要求系统目录 {path}。");
                var version = FileVersionInfo.GetVersionInfo(actual.ToString());
                // MSVC 14.40 introduced constexpr mutex initialization used by NI 2026 ICU.
                if (version.FileMajorPart < 14 || version.FileMajorPart == 14 && version.FileMinorPart < 40)
                    throw new InvalidOperationException($"系统 C++ x64 运行库过旧：{path} {version.FileVersion}，请安装 NI 2026 支持的新版运行库。");
                Console.Error.WriteLine($"LabVIEW bridge runtime: {actual}, version={version.FileVersion}");
            }
            VerifyLoadedRuntimePaths();
            SystemRuntimeModules.AddRange(loaded);
        }
        catch
        {
            foreach (var native in loaded) native.Dispose();
            throw;
        }
    }

    private static void VerifyLoadedRuntimePaths()
    {
        using var process = Process.GetCurrentProcess();
        foreach (ProcessModule loaded in process.Modules)
        {
            string name = loaded.ModuleName;
            if (!name.Equals("msvcp140.dll", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("vcruntime140.dll", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("vcruntime140_1.dll", StringComparison.OrdinalIgnoreCase)) continue;
            var expected = Path.Combine(Environment.SystemDirectory, name);
            if (!string.Equals(Path.GetFullPath(loaded.FileName), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"辅助进程已有第三方 C++ 运行库 {loaded.FileName}，为避免 NI 运行库冲突已拒绝加载。要求 {expected}。");
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileName(IntPtr module, StringBuilder path, int size);

    private sealed class ModuleHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal ModuleHandle(IntPtr native) : base(true) => SetHandle(native);
        protected override bool ReleaseHandle()
        {
            NativeLibrary.Free(handle);
            return true;
        }
    }
}
