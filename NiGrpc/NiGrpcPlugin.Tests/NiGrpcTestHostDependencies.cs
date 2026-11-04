using System.Reflection;
using System.Runtime.CompilerServices;

namespace NiGrpc.Tests;

internal static class NiGrpcTestHostDependencies
{
    // Match the platform's expression-host references before the SDK creates its
    // first evaluator. Without this, editor-only filtering depends on whether an
    // unrelated protocol/JSON test happened to load System.Text.Json first.
    // Do not replace the real evaluator or suppress expression diagnostics.
    [ModuleInitializer]
    internal static void LoadExpressionHostReferences() => Assembly.Load("System.Text.Json");
}
