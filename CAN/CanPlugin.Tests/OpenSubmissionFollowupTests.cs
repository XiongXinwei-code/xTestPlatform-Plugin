using System.Reflection;
using CAN.Adapters;
using CAN.Executors;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class OpenSubmissionFollowupTests
{
    private static int _channel = 800000;

    [Theory]
    [InlineData("marker")]
    [InlineData("cancel")]
    [InlineData("CleanupExecution")]
    [InlineData("CleanupAll")]
    [InlineData("normal")]
    public async Task Registered_open_rechecks_execution_identity_at_its_actual_driver_entry(string transition)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        using var releaseGate = new ManualResetEventSlim();
        using var gateEntered = new ManualResetEventSlim();
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var registry = DispatchProxy.Create<IResourceRegistry, RegisteredOpenRegistry>();
        var proxy = (RegisteredOpenRegistry)(object)registry; proxy.Inner = actual;
        var raw = new OpeningDriver();
        Task holding = Task.CompletedTask;
        bool reachedBoundary = false;
        proxy.AfterRegisteredEpochSnapshot = owner =>
        {
            reachedBoundary = true;
            if (transition is "marker" or "cancel" or "normal")
            {
                var gate = typeof(CanConnectionResource).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
                holding = Task.Factory.StartNew(() =>
                {
                    lock (gate) { gateEntered.Set(); if (!releaseGate.Wait(10000)) throw new TimeoutException("test Open admission gate"); }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Assert.True(gateEntered.Wait(5000));
            }
            // SDK removal may precede Dispose of its remaining entries. The current
            // lookup already returned the captured marker; this is that intermediate
            // state, not a claim that Remove performs complete SDK cleanup.
            if (transition == "marker") Assert.True(actual.Remove("PluginCommon.ExecutionEpoch:CAN"));
            else if (transition == "cancel") caller.Cancel();
            else if (transition is "CleanupExecution" or "CleanupAll") Cleanup(actual, transition);
            releaseGate.Set();
        };
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        var state = (SdkCanContext)(object)context;
        state.Registry = registry; state.Plugin = new CanOpenPlugin();
        state.Setting = new CanOpenSetting { AdapterType = CanAdapterType.Kvaser,
            Channel = $"\"{Interlocked.Increment(ref _channel)}\"", TimeoutMs = 5000 };
        try
        {
            var result = await new CanOpenExecutor(_ => raw).ExecuteAsync(context, caller.Token).WaitAsync(guard.Token);
            Assert.True(reachedBoundary);
            Assert.Equal(transition == "normal" ? TestStatus.Completed : transition == "cancel" ? TestStatus.Aborted : TestStatus.Error,
                result.StepResult.Status);
            Assert.Equal(transition == "normal" ? 1 : 0, raw.Opens);
            if (transition == "marker") Assert.Contains("执行资源作用域已回收", result.StepResult.Error!.Message);
        }
        finally
        {
            releaseGate.Set(); await holding;
            if (proxy.Owner != null) await proxy.Owner.RequestClose().WaitAsync(guard.Token);
            Cleanup(actual, "CleanupAll");
        }
    }

    private static void Cleanup(IResourceRegistry registry, string method) => registry.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);

    private sealed class OpeningDriver : ICanAdapter
    {
        internal int Opens;
        public bool IsConnected { get; private set; }
        public void Open(CanAdapterConfig config) { Interlocked.Increment(ref Opens); IsConnected = true; }
        public void Close() => IsConnected = false;
        public void Dispose() => Close();
        public void Write(CanMessage message) { }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
    }
}

public class RegisteredOpenRegistry : DispatchProxy
{
    public IResourceRegistry Inner = null!;
    public CanConnectionResource? Owner;
    public Action<CanConnectionResource>? AfterRegisteredEpochSnapshot;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var result = method!.Invoke(Inner, args);
        if (method.Name == "TryAdd" && Equals(args?[0], CanHelper.GetAdapterKey("CAN1")) && result is true)
            Owner = (CanConnectionResource)args![1]!;
        else if (method.Name == "TryGet" && Equals(args?[0], "PluginCommon.ExecutionEpoch:CAN") && Owner != null)
        {
            var callback = AfterRegisteredEpochSnapshot; AfterRegisteredEpochSnapshot = null;
            callback?.Invoke(Owner);
        }
        return result;
    }
}
