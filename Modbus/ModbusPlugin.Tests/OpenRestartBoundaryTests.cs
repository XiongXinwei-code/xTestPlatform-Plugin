using System.Reflection;
using Modbus.Executors;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Engine;
using Xunit;

namespace Modbus.ContractTests;

public sealed class OpenRestartBoundaryTests
{
    [Fact]
    public async Task Close_during_queued_restart_prevents_late_registration_of_that_open_generation()
    {
        using var guard = new CancellationTokenSource(15000); using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int opens = 0;
        var executor = new ModbusConnectExecutor(async (owner, _, _, token) =>
        {
            owner.OwnTransport(new NoopTransport()); owner.OwnMaster(DispatchProxy.Create<IModbusMaster, PublicationBoundaryTests.ControlledMaster>());
            Interlocked.Increment(ref opens); entered.TrySetResult(); await release.Task;
        });
        var setting = new ModbusConnectSetting { ConnectionName = "\"C\"", IpAddress = "\"127.0.0.1\"", TimeoutMs = 2000, TcpPort = Random.Shared.Next(30000, 50000) };
        var (context, state) = PublicationContextProxy.Create(new ModbusConnectPlugin(), setting);
        string key = ModbusHelper.GetConnectionKey("C");
        var first = executor.ExecuteAsync(context, caller.Token);
        Task<ExecutionResult>? restarting = null, closing = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await first.WaitAsync(guard.Token)).StepResult.Status);
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            state.Store.OnTryGet = (name, type) => { if (name == key && type == typeof(object)) captured.TrySetResult(); };
            restarting = executor.ExecuteAsync(context, guard.Token);
            await captured.Task.WaitAsync(guard.Token);
            long generation = ModbusHelper.GetCloseGeneration(context.Resources, key);
            var (closeContext, _) = PublicationContextProxy.Create(new ModbusDisconnectPlugin(), new ModbusDisconnectSetting { ConnectionName = "\"C\"", TimeoutMs = 2000 }, state.Store);
            closing = new ModbusDisconnectExecutor().ExecuteAsync(closeContext, guard.Token);
            while (ModbusHelper.GetCloseGeneration(context.Resources, key) == generation) await Task.Delay(2, guard.Token);
            release.TrySetResult();
            Assert.Equal(TestStatus.Error, (await restarting.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(TestStatus.Completed, (await closing.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(1, opens); Assert.False(state.Store.Values.ContainsKey(key));
        }
        finally
        {
            release.TrySetResult(); await first.WaitAsync(guard.Token);
            if (restarting != null) await restarting.WaitAsync(guard.Token);
            if (closing != null) await closing.WaitAsync(guard.Token);
            if (state.Store.Values.TryGetValue(key, out var current) && current is ModbusConnectionResource owner) await owner.CloseAsync(2000, default);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Interrupted_open_preserves_cancellation_and_first_same_registry_restart(bool checkCancellation, bool restartBeforeExit)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int opens = 0;
        var executor = new ModbusConnectExecutor(async (owner, _, _, token) =>
        {
            owner.OwnTransport(new NoopTransport());
            owner.OwnMaster(DispatchProxy.Create<IModbusMaster, PublicationBoundaryTests.ControlledMaster>());
            if (Interlocked.Increment(ref opens) == 1) { entered.TrySetResult(); await release.Task; }
        });
        var setting = new ModbusConnectSetting { ConnectionName = "\"C\"", IpAddress = "\"127.0.0.1\"", TcpPort = Random.Shared.Next(30000, 50000), TimeoutMs = 300 };
        var (context, state) = PublicationContextProxy.Create(new ModbusConnectPlugin(), setting);
        string key = ModbusHelper.GetConnectionKey("C");
        var first = executor.ExecuteAsync(context, caller.Token);
        ModbusConnectionResource? old = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            old = Assert.IsType<ModbusConnectionResource>(state.Store.Values[key]);
            caller.Cancel();
            var interrupted = await first.WaitAsync(guard.Token);
            Assert.Same(old, state.Store.Values[key]);
            Assert.False(old.IsClosed);
            if (checkCancellation) Assert.Equal(TestStatus.Aborted, interrupted.StepResult.Status);
            Task<ExecutionResult>? queuedRestart = restartBeforeExit ? executor.ExecuteAsync(context, guard.Token) : null;
            if (queuedRestart != null)
            {
                await Task.WhenAny(queuedRestart, Task.Delay(40, guard.Token));
                Assert.False(queuedRestart.IsCompleted);
                Assert.Equal(1, opens);
            }
            release.TrySetResult();
            await old.CloseAsync(2000, default);
            if (!checkCancellation)
            {
                var restarted = await (queuedRestart ?? executor.ExecuteAsync(context, guard.Token));
                Assert.Equal(TestStatus.Completed, restarted.StepResult.Status);
                Assert.Equal(2, opens);
                Assert.NotSame(old, state.Store.Values[key]);
            }
        }
        finally
        {
            release.TrySetResult();
            await first.WaitAsync(guard.Token);
            if (old != null) await old.CloseAsync(2000, default);
            if (state.Store.Values.TryGetValue(key, out var current) && current is ModbusConnectionResource owner)
                await owner.CloseAsync(2000, default);
        }
    }
    private sealed class NoopTransport : IDisposable { public void Dispose() { } }
}
