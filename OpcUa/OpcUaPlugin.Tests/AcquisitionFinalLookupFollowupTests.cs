using System.Reflection;
using Opc.Ua;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace OpcUa.Tests;

public sealed class AcquisitionFinalLookupFollowupTests
{
    [Theory]
    [InlineData("cancel", false)] [InlineData("cancel", true)]
    [InlineData("CleanupExecution", false)] [InlineData("CleanupExecution", true)]
    [InlineData("CleanupAll", false)] [InlineData("CleanupAll", true)]
    [InlineData("none", false)] [InlineData("none", true)]
    public async Task Final_acquisition_lookup_rechecks_caller_and_epoch_before_reporting_completion(string transition, bool found)
    {
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var forwarding = DispatchProxy.Create<IResourceRegistry, EpochForwardingRegistry>();
        var boundary = (EpochForwardingRegistry)(object)forwarding; boundary.Inner = actual;
        var items = new List<OpcUaDataAcqItem> { new() { NodeId = "ns=2;s=V" } };
        var setting = new OpcUaDataAcqStartSetting
        { ConnectionName = "\"C\"", TaskName = "\"T\"", TimeoutMs = 5000, SamplingIntervalMs = 100, Items = [.. items] };
        var (context, state) = BoundaryContext.Create(new OpcUaDataAcqStartPlugin(), setting); state.UseRegistry(forwarding);
        var connection = new OpcUaConnectionResource(() => Task.CompletedTask, 1000)
        { AcquisitionReader = (_, _) => Task.FromResult(new DataValueCollection { new DataValue(1) }) };
        var fresh = new OpcUaDataAcqTask("fresh", _ => Task.FromResult(new DataValueCollection { new DataValue(2) }), items, 100, 0, 100);
        Assert.True(actual.TryAdd("OpcUa_C", connection, ResourceLifetime.Execution));
        using var caller = new CancellationTokenSource();
        bool armed = false, entered = false; OpcUaDataAcqTask? created = null;
        // The successful-start log precedes EnsureCurrent(connection), then the
        // final task lookup. Earlier task lookups must retain their real behavior.
        state.OnLog = () => armed = true;
        boundary.BeforeNamedLookup = key =>
        {
            if (key != "OpcUaDataAcq_T" || !armed || entered) return;
            entered = true;
            Assert.True(actual.TryGet<OpcUaDataAcqTask>(key, out created));
            if (transition.StartsWith("Cleanup", StringComparison.Ordinal))
            {
                actual.GetType().GetMethod(transition, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
                if (found) Assert.True(actual.TryAdd(key, fresh, ResourceLifetime.Execution));
            }
            else
            {
                if (!found) actual.Remove(key, dispose: false);
                if (transition == "cancel") caller.Cancel();
            }
        };
        try
        {
            var result = await new OpcUaDataAcqStartPlugin().CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.True(entered); Assert.NotNull(created);
            var expected = transition == "cancel" ? TestStatus.Aborted : transition == "none" && found ? TestStatus.Completed : TestStatus.Error;
            Assert.Equal(expected, result.StepResult.Status);
            if (transition.StartsWith("Cleanup", StringComparison.Ordinal)) Assert.Contains("资源作用域已回收", result.StepResult.Error!.Message);
            if (expected != TestStatus.Completed) Assert.True(created.IsStopRequested);
            Assert.False(fresh.IsStopRequested); Assert.Empty(state.Published);
        }
        finally
        {
            state.OnLog = null; boundary.BeforeNamedLookup = null;
            created?.Dispose(); fresh.Dispose();
            if (created != null) await created.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await fresh.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await connection.CloseAsync(2000);
            actual.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
        }
    }
}
