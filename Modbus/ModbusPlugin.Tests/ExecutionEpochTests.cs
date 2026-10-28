using System.Reflection;
using System.Runtime.ExceptionServices;
using Modbus.Executors;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace Modbus.ContractTests;

public sealed class ExecutionEpochTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_write_with_pending_name_expression_cannot_use_owner_from_next_sdk_execution(bool cleanupAll)
    {
        using var guard = new CancellationTokenSource(15000);
        var actual = SdkRegistryForwarder.NewActual();
        var name = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new ModbusWriteExecutor((expression, _) =>
        {
            if (expression == "\"C\"") { entered.TrySetResult(); return name.Task; }
            return Task.FromResult(expression.Trim('"'));
        });
        var (context, state) = PublicationContextProxy.Create(new ModbusWritePlugin(), new ModbusWriteSetting { ConnectionName = "\"C\"", Values = "1" });
        state.ResourcesOverride = actual;
        var execution = executor.ExecuteAsync(context, guard.Token);
        await entered.Task.WaitAsync(guard.Token);
        SdkRegistryForwarder.Cleanup(actual, cleanupAll);
        var master = DispatchProxy.Create<IModbusMaster, CounterMaster>();
        var counter = (CounterMaster)(object)master;
        var fresh = new ModbusConnectionResource(master, new Handle(() => { }), 1000);
        string key = ModbusHelper.GetConnectionKey("C"); actual.Set(key, fresh, ResourceLifetime.Execution);
        try
        {
            name.SetResult("C");
            Assert.Equal(TestStatus.Error, (await execution.WaitAsync(guard.Token)).StepResult.Status);
            Assert.False(guard.IsCancellationRequested); Assert.Equal(0, counter.Writes);
            Assert.True(actual.TryGet<ModbusConnectionResource>(key, out var current)); Assert.Same(fresh, current); Assert.False(fresh.IsClosed);
        }
        finally { name.TrySetResult("C"); await execution.WaitAsync(guard.Token); await fresh.CloseAsync(1000, default); SdkRegistryForwarder.Cleanup(actual, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sdk_cleanup_freezes_old_Open_waiting_for_previous_owner_and_first_new_execution_can_open(bool cleanupAll)
    {
        using var guard = new CancellationTokenSource(15000);
        using var release = new ManualResetEventSlim();
        var disposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actual = SdkRegistryForwarder.NewActual();
        var registry = SdkRegistryForwarder.Wrap(actual, out var routing);
        string key = ModbusHelper.GetConnectionKey("C");
        var old = new ModbusConnectionResource(Master(), new Handle(() => { disposing.TrySetResult(); release.Wait(guard.Token); }), 4000);
        actual.Set(key, old, ResourceLifetime.Execution);
        old.Dispose(); await disposing.Task.WaitAsync(guard.Token);
        routing.OnTryGet = (name, type) => { if (name == key && type == typeof(object)) captured.TrySetResult(); };
        int opens = 0;
        var executor = new ModbusConnectExecutor((owner, _, _, _) =>
        {
            Interlocked.Increment(ref opens); owner.OwnTransport(new Handle(() => { })); owner.OwnMaster(Master()); return Task.CompletedTask;
        });
        var (context, state) = PublicationContextProxy.Create(new ModbusConnectPlugin(), new ModbusConnectSetting
        { ConnectionName = "\"C\"", IpAddress = "\"127.0.0.1\"", TcpPort = Random.Shared.Next(30000, 50000), TimeoutMs = 4000 });
        state.ResourcesOverride = registry;
        var scheduler = new HeldContinuationContext();
        var opening = scheduler.Start(() => executor.ExecuteAsync(context, guard.Token));
        try
        {
            // The callback runs in the real helper's locked previous-owner snapshot;
            // this continuation then reaches its incomplete CloseAsync before yielding.
            await scheduler.PumpUntilAsync(captured.Task, opening, guard.Token);
            Assert.False(opening.IsCompleted); Assert.Equal(0, opens);
            SdkRegistryForwarder.Cleanup(actual, cleanupAll); // Do not cancel the step token.
            release.Set(); await old.CloseAsync(2000, guard.Token);
            await scheduler.DrainUntilAsync(opening, guard.Token);
            var result = await opening;
            Assert.False(guard.IsCancellationRequested);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, opens);
            Assert.False(actual.TryGet<ModbusConnectionResource>(key, out _));

            var fresh = await executor.ExecuteAsync(context, guard.Token).WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Completed, fresh.StepResult.Status);
            Assert.Equal(1, opens);
            Assert.True(actual.TryGet<ModbusConnectionResource>(key, out var current));
            Assert.NotSame(old, current); Assert.False(current.IsClosed);
        }
        finally
        {
            release.Set(); await scheduler.DrainUntilAsync(opening, guard.Token);
            if (actual.TryGet<ModbusConnectionResource>(key, out var owner)) await owner.CloseAsync(2000, default);
            SdkRegistryForwarder.Cleanup(actual, true);
        }
    }

    private static IModbusMaster Master() => DispatchProxy.Create<IModbusMaster, PublicationBoundaryTests.ControlledMaster>();
    public class CounterMaster : DispatchProxy
    {
        internal int Writes;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name.StartsWith("Write", StringComparison.Ordinal)) { Interlocked.Increment(ref Writes); return Task.CompletedTask; }
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    private sealed class Handle(Action action) : IDisposable { public void Dispose() => action(); }
}

public class SdkRegistryForwarder : DispatchProxy
{
    private IResourceRegistry _actual = null!;
    internal Action<string, Type>? OnTryGet;
    internal static IResourceRegistry NewActual() => (IResourceRegistry)Activator.CreateInstance(
        typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    internal static IResourceRegistry Wrap(IResourceRegistry actual, out SdkRegistryForwarder routing)
    {
        var registry = Create<IResourceRegistry, SdkRegistryForwarder>(); routing = (SdkRegistryForwarder)(object)registry; routing._actual = actual; return registry;
    }
    internal static void Cleanup(IResourceRegistry actual, bool all) => actual.GetType()
        .GetMethod(all ? "CleanupAll" : "CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet") OnTryGet?.Invoke((string)args![0]!, method.GetGenericArguments()[0]);
        try { return method.Invoke(_actual, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
