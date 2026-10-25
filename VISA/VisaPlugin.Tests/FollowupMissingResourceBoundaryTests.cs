using System.Reflection;
using VISA.Helpers;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace VISA.Tests;

public sealed class FollowupMissingResourceBoundaryTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (string step in new[] { "Query", "Read", "Write", "WaitOpc", "BatchWrite" })
        foreach (string transition in new[] { "cancel", "CleanupExecution", "CleanupAll", "none" })
            yield return new object[] { step, transition };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Missing_session_lookup_preserves_original_cancellation_and_execution_diagnostics(string step, string transition)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(VisaReadPlugin).Assembly.GetType("VISA.Visa" + step + "Plugin", true)!)!;
        object setting = plugin.CreateSerializer().CreateDefault();
        setting.GetType().GetProperty("ConnectionName")!.SetValue(setting, "\"C\"");
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var registry = DispatchProxy.Create<IResourceRegistry, LookupBoundaryRegistry>();
        var boundary = (LookupBoundaryRegistry)(object)registry;
        boundary.Inner = actual;
        state.ResourcesOverride = registry;
        using var caller = new CancellationTokenSource();
        int lookups = 0;
        boundary.BeforeSessionLookup = () =>
        {
            lookups++;
            if (transition == "cancel") caller.Cancel();
            else if (transition != "none") Cleanup(actual, transition);
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.Equal(1, lookups);
            Assert.Empty(state.Published);
            // No native session is registered: no hardware API can be reached.
            Assert.False(actual.Contains(VisaHelper.GetSessionKey("C")));
            if (transition == "cancel") Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            else
            {
                Assert.Equal(TestStatus.Error, result.StepResult.Status);
                Assert.Contains(transition == "none" ? "未找到 VISA 会话" : "资源作用域已回收", result.StepResult.Error!.Message);
                // A fresh execution with an absent name must recover the ordinary
                // diagnostic, rather than retaining the retired epoch forever.
                boundary.BeforeSessionLookup = null;
                var next = await plugin.CreateExecutor().ExecuteAsync(context);
                Assert.Equal(TestStatus.Error, next.StepResult.Status);
                Assert.Contains("未找到 VISA 会话", next.StepResult.Error!.Message);
            }
        }
        finally { boundary.BeforeSessionLookup = null; Cleanup(actual, "CleanupAll"); }
    }

    private static void Cleanup(IResourceRegistry registry, string operation) =>
        registry.GetType().GetMethod(operation, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
}
