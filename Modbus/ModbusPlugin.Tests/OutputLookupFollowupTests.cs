using System.Reflection;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace Modbus.ContractTests;

[Collection("Modbus followup isolated")]
public sealed class OutputLookupFollowupTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (bool batch in new[] { false, true })
            foreach (string lookup in new[] { "Has", "Get" })
                foreach (string boundary in new[] { "cancel", "CleanupExecution", "CleanupAll", "invalid" })
                    yield return [batch, lookup, boundary];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Output_preflight_checks_original_stop_and_scope_after_both_variable_queries(bool batch, string lookup, string boundary)
    {
        IStepPlugin plugin = batch ? new ModbusBatchReadPlugin() : new ModbusReadPlugin();
        object setting = batch ? new ModbusBatchReadSetting { ConnectionName = "\"C\"", Items = [new() { ResultVariable = "Locals.rx" }] } :
            new ModbusReadSetting { ConnectionName = "\"C\"", ResultVariable = "Locals.rx" };
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        var resources = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        state.ResourcesOverride = resources; state.Variables = new() { ["Locals.rx"] = (ushort)0 };
        var native = DispatchProxy.Create<IModbusMaster, RequestPreflightFollowupTests.Probe>();
        var probe = (RequestPreflightFollowupTests.Probe)(object)native;
        var owner = new ModbusConnectionResource(native, new Noop(), 2000);
        Assert.True(resources.TryAdd(ModbusHelper.GetConnectionKey("C"), owner));
        using var caller = new CancellationTokenSource();
        bool entered = false;
        void Boundary(string path)
        {
            if (path != "Locals.rx" || entered) return;
            entered = true;
            if (boundary == "cancel") caller.Cancel();
            else if (boundary != "invalid") Cleanup(resources, boundary);
            if (lookup == "Has") state.Variables.Clear(); else state.Variables["Locals.rx"] = "wrong type";
        }
        if (lookup == "Has") state.BeforeHasVariable = Boundary; else state.BeforeGetVariable = Boundary;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.True(entered);
            Assert.Equal(boundary == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (boundary.StartsWith("Cleanup", StringComparison.Ordinal))
                Assert.Contains("作用域", result.StepResult.Error!.Message + result.StepResult.Error.Detail);
            Assert.Equal(0, probe.Calls); Assert.Empty(state.Published);
        }
        finally { Cleanup(resources, "CleanupAll"); await owner.CloseAsync(2000, default); }
    }
    private static void Cleanup(IResourceRegistry resources, string name) =>
        resources.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(resources, null);
    private sealed class Noop : IDisposable { public void Dispose() { } }
}
