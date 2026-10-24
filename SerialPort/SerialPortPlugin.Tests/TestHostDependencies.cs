using System.Reflection;
using System.Runtime.CompilerServices;

namespace SerialPort.Tests;

internal static class TestHostDependencies
{
    [ModuleInitializer]
    internal static void LoadSdkDefaultScriptImports()
    {
        // SDK 在首次创建 evaluator 时快照程序集；真实宿主已加载的默认 import 依赖。
        _ = Assembly.Load("System.Text.Json");
    }
}
