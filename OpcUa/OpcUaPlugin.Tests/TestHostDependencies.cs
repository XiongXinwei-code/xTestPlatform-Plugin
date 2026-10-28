using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpcUa.Tests;

internal static class TestHostDependencies
{
    [ModuleInitializer]
    internal static void LoadSdkScriptImportsBeforeAnyEditorConstructsTheEvaluator()
    {
        // xTest SDK snapshots loaded reference assemblies when its evaluator is first constructed.
        // 独立测试宿主需先加载 SDK 默认 imports 依赖，避免测试顺序改变 reference snapshot。
        _ = Assembly.Load("System.Text.Json");
    }
}
