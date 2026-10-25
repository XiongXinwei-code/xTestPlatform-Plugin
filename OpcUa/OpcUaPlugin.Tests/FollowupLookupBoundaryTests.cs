using System.Reflection;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Executors;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace OpcUa.Tests;

public sealed class FollowupLookupBoundaryTests
{
    public static IEnumerable<object[]> MissingCases()
    {
        foreach (string step in new[] { "Read", "Write", "BatchRead", "BatchWrite", "Subscribe", "DataAcqStart", "DataAcqRead", "DataAcqStop" })
        foreach (string transition in new[] { "cancel", "CleanupExecution", "CleanupAll", "none" })
            yield return new object[] { step, transition };
    }

    [Theory]
    [MemberData(nameof(MissingCases))]
    public async Task Missing_lookup_keeps_original_cancellation_and_execution_diagnostics(string step, string transition)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(OpcUaReadPlugin).Assembly.GetType("OpcUa.OpcUa" + step + "Plugin", true)!)!;
        object setting = plugin.CreateSerializer().CreateDefault();
        setting.GetType().GetProperty("ConnectionName")?.SetValue(setting, "\"C\"");
        setting.GetType().GetProperty("TaskName")?.SetValue(setting, "\"T\"");
        var (context, state) = BoundaryContext.Create(plugin, setting);
        var actual = NewRegistry();
        var forwarding = DispatchProxy.Create<IResourceRegistry, EpochForwardingRegistry>();
        var boundary = (EpochForwardingRegistry)(object)forwarding;
        boundary.Inner = actual; state.UseRegistry(forwarding);
        using var caller = new CancellationTokenSource();
        string key = step is "DataAcqRead" or "DataAcqStop" ? "OpcUaDataAcq_T" : "OpcUa_C";
        int lookups = 0;
        boundary.BeforeNamedLookup = lookup =>
        {
            if (lookup != key) return;
            lookups++;
            if (transition == "cancel") caller.Cancel();
            else if (transition != "none") Cleanup(actual, transition);
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.Equal(1, lookups); Assert.Empty(state.Published);
            Assert.False(actual.Contains(key)); // No Session/collector/native I/O exists.
            Assert.Equal(transition == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (transition != "cancel")
            {
                Assert.Contains(transition == "none" ? "不存在" : "资源作用域已回收", result.StepResult.Error!.Message);
                boundary.BeforeNamedLookup = null;
                var next = await plugin.CreateExecutor().ExecuteAsync(context);
                Assert.Equal(TestStatus.Error, next.StepResult.Status);
                Assert.Contains("不存在", next.StepResult.Error!.Message);
            }
        }
        finally { boundary.BeforeNamedLookup = null; Cleanup(actual, "CleanupAll"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_connect_cannot_close_a_fresh_session_returned_across_sdk_cleanup(bool all)
    {
        var actual = NewRegistry();
        var forwarding = DispatchProxy.Create<IResourceRegistry, EpochForwardingRegistry>();
        var boundary = (EpochForwardingRegistry)(object)forwarding; boundary.Inner = actual;
        var (context, state) = BoundaryContext.Create(new OpcUaConnectPlugin(), new OpcUaConnectSetting
        { ConnectionName = "\"C\"", EndpointUrl = "\"opc.tcp://unit.test:4840\"", TimeoutMs = 5000 });
        state.UseRegistry(forwarding);
        int closes = 0, opens = 0;
        var fresh = new OpcUaConnectionResource(() => { Interlocked.Increment(ref closes); return Task.CompletedTask; }, 1000);
        bool replaced = false;
        boundary.BeforeNamedLookup = key =>
        {
            if (key != "OpcUa_C" || replaced) return;
            replaced = true; Cleanup(actual, all ? "CleanupAll" : "CleanupExecution");
            Assert.True(actual.TryAdd(key, fresh, ResourceLifetime.Execution));
        };
        try
        {
            var result = await new OpcUaConnectExecutor(_ =>
            { opens++; return Task.FromException<Session>(new InvalidOperationException("No new Session may be created")); }).ExecuteAsync(context);
            Assert.True(replaced); Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, opens); Assert.Equal(0, closes); Assert.False(fresh.IsClosed);
            Assert.True(actual.TryGet<OpcUaConnectionResource>("OpcUa_C", out var current)); Assert.Same(fresh, current);
            fresh.EnsureAvailableForPublication();
        }
        finally { boundary.BeforeNamedLookup = null; await fresh.CloseAsync(2000); Cleanup(actual, "CleanupAll"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_acquisition_start_cannot_stop_a_fresh_collector_returned_across_cleanup(bool all)
    {
        var actual = NewRegistry();
        var forwarding = DispatchProxy.Create<IResourceRegistry, EpochForwardingRegistry>();
        var boundary = (EpochForwardingRegistry)(object)forwarding; boundary.Inner = actual;
        var setting = new OpcUaDataAcqStartSetting
        { ConnectionName = "\"C\"", TaskName = "\"T\"", TimeoutMs = 5000, Items = [new() { NodeId = "ns=2;s=V" }] };
        var (context, state) = BoundaryContext.Create(new OpcUaDataAcqStartPlugin(), setting); state.UseRegistry(forwarding);
        var oldConnection = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        var freshConnection = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        var fresh = new OpcUaDataAcqTask("T", _ => Task.FromResult(new DataValueCollection { new DataValue(1) }),
            setting.Items.ToList(), 100, 0, 100); // Private fake collector; never calls an OPC Session.
        Assert.True(actual.TryAdd("OpcUa_C", oldConnection, ResourceLifetime.Execution));
        bool replaced = false;
        boundary.BeforeNamedLookup = key =>
        {
            if (key != "OpcUaDataAcq_T" || replaced) return;
            replaced = true; Cleanup(actual, all ? "CleanupAll" : "CleanupExecution");
            Assert.True(actual.TryAdd("OpcUa_C", freshConnection, ResourceLifetime.Execution));
            Assert.True(actual.TryAdd(key, fresh, ResourceLifetime.Execution));
        };
        try
        {
            var result = await new OpcUaDataAcqStartPlugin().CreateExecutor().ExecuteAsync(context);
            Assert.True(replaced); Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.False(fresh.IsStopRequested);
            Assert.True(actual.TryGet<OpcUaDataAcqTask>("OpcUaDataAcq_T", out var current)); Assert.Same(fresh, current);
            Assert.False(freshConnection.IsClosed);
        }
        finally
        {
            boundary.BeforeNamedLookup = null; fresh.Dispose();
            await fresh.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await oldConnection.CloseAsync(2000); await freshConnection.CloseAsync(2000); Cleanup(actual, "CleanupAll");
        }
    }

    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
        .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry, string method) =>
        registry.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
}
