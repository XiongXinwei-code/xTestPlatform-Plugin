using System.Reflection;
using AuditTestSupport;
using NiDaq.Helpers;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class FollowupMissingTaskBoundaryTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (string step in new[] { "AiRead", "SyncRead", "EncoderRead", "AoWriteSingle", "TaskStart", "TaskStop" })
        foreach (string transition in new[] { "cancel", "CleanupExecution", "CleanupAll", "none" })
            yield return new object[] { step, transition };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Missing_task_lookup_preserves_original_cancellation_and_execution_diagnostics(string step, string transition)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(NiDaqAiReadPlugin).Assembly.GetType("NiDaq.NiDaq" + step + "Plugin", true)!)!;
        object setting = plugin.CreateSerializer().CreateDefault();
        setting.GetType().GetProperty("TaskName")!.SetValue(setting, "\"T\"");
        setting.GetType().GetProperty("ResultVariable")?.SetValue(setting, "Locals.rx");
        setting.GetType().GetProperty("Values")?.SetValue(setting, "0.5");
        var (context, state) = ContextProxy.Create(plugin, setting);
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var registry = DispatchProxy.Create<IResourceRegistry, EpochForwardingRegistry>();
        var boundary = (EpochForwardingRegistry)(object)registry; boundary.Inner = actual; state.Store = registry;
        using var caller = new CancellationTokenSource();
        int lookups = 0;
        boundary.BeforeNamedLookup = key =>
        {
            if (key != "NiDaq.Task:T") return;
            lookups++;
            if (transition == "cancel") caller.Cancel();
            else if (transition != "none") Cleanup(actual, transition);
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.Equal(1, lookups); Assert.Empty(state.Published);
            Assert.False(actual.Contains("NiDaq.Task:T")); // No DAQ task/driver call exists.
            Assert.Equal(transition == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (transition != "cancel")
            {
                Assert.Contains(transition == "none" ? "未找到当前序列" : "资源作用域已回收", result.StepResult.Error!.Message);
                boundary.BeforeNamedLookup = null;
                var next = await plugin.CreateExecutor().ExecuteAsync(context);
                Assert.Equal(TestStatus.Error, next.StepResult.Status);
                Assert.Contains("未找到当前序列", next.StepResult.Error!.Message);
            }
        }
        finally { boundary.BeforeNamedLookup = null; Cleanup(actual, "CleanupAll"); }
    }

    private static void Cleanup(IResourceRegistry registry, string method) =>
        registry.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
}
