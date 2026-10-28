using System.Reflection;
using LIN.Adapters;
using LIN.Models;
using LIN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace LIN.Tests;

public sealed class QueuedEpochReauditTests
{
    [Fact]
    public async Task SuccessfulOpenLogCannotExtendTotalBudget()
    {
        var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
        state.Plugin = new LinOpenPlugin(); state.Setting = new LinOpenSetting { Channel = "\"log-budget\"", TimeoutMs = 1000 };
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>("\"log-budget\"", context);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>("\"LIN1\"", context);
        bool logged = false;
        state.OnLog = text => { if (text.Contains("LIN 通道已打开")) { logged = true; Thread.Sleep(1100); } };
        var result = await new LIN.Executors.LinOpenExecutor(_ => new Adapter()).ExecuteAsync(context);
        try { Assert.True(logged); Assert.Equal(TestStatus.Error, result.StepResult.Status); }
        finally { if (state.Items.TryGetValue(LIN.Helpers.LinHelper.GetAdapterKey("LIN1"), out var owner)) await ((LinConnectionResource)owner).CloseAsync(); }
    }

    [Fact]
    public async Task ActualWriteCannotSubmitAfterSdkRetiresItsEpochWhileRawDisposeIsPending()
    {
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var forwarding = DispatchProxy.Create<IResourceRegistry, ArmLinRegistry>(); var proxy = (ArmLinRegistry)(object)forwarding; proxy.Inner = actual;
        var context = DispatchProxy.Create<IExecutionContext, SdkLinContext>(); var state = (SdkLinContext)(object)context; state.Registry = forwarding;
        state.Plugin = new LinWritePlugin(); state.Setting = new LinWriteSetting { FrameId = "\"0x12\"", Data = "\"01\"" };
        using var releaseWorker = new ManualResetEventSlim(); using var releaseDispose = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new Adapter { ReleaseDispose = releaseDispose }; actual.Set(LIN.Helpers.LinHelper.GetAdapterKey("LIN1"), raw, ResourceLifetime.Execution);
        int held = 0;
        var flow = new AsyncLocal<bool>(change =>
        {
            if (change.ThreadContextChanged && change.CurrentValue && Interlocked.CompareExchange(ref held, 1, 0) == 0)
            { entered.TrySetResult(); releaseWorker.Wait(5000); }
        });
        // The third adapter lookup is the final guard immediately before Task.Run.
        proxy.Arm = () => flow.Value = true;
        var operation = state.Plugin.CreateExecutor().ExecuteAsync(context);
        flow.Value = false;
        Task? cleanup = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cleanup = Task.Run(() => actual.GetType().GetMethod("CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null));
            await raw.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            releaseWorker.Set();
            Assert.Equal(TestStatus.Error, (await operation).StepResult.Status);
            Assert.Equal(0, raw.Writes);
        }
        finally
        {
            releaseWorker.Set(); releaseDispose.Set(); try { await operation; } catch { }
            if (cleanup != null) await cleanup;
            await LinConnectionResource.For(raw).CloseAsync();
        }
    }

    private sealed class Adapter : ILinAdapter
    {
        public ManualResetEventSlim? ReleaseDispose; public int Writes; public bool IsConnected => true;
        public readonly TaskCompletionSource DisposeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Open(LinAdapterConfig c) { } public void Close() { }
        public void Dispose() { DisposeEntered.TrySetResult(); if (ReleaseDispose != null && !ReleaseDispose.Wait(5000)) throw new TimeoutException("fixture disposal barrier"); }
        public void Write(LinFrame f) => Interlocked.Increment(ref Writes);
        public LinFrame? Read(int t, CancellationToken ct = default) => null;
        public LinFrame? Read(byte id, int t, CancellationToken ct = default) => null;
        public void Sleep(bool remote = true) { } public void Wakeup(bool remote = true) { }
    }
}

public class ArmLinRegistry : DispatchProxy
{
    public IResourceRegistry Inner = null!; public Action? Arm; private int count;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var result = method!.Invoke(Inner, args);
        if (method.Name == "TryGet" && method.GetGenericArguments()[0] == typeof(ILinAdapter) && ++count == 3) Arm?.Invoke();
        return result;
    }
}
