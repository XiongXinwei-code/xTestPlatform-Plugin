using System.Reflection;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace Modbus.ContractTests;

public sealed class PublicationBoundaryTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Read_does_not_publish_after_cancellation_or_owner_replacement(bool batch, bool cancel)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        var native = DispatchProxy.Create<IModbusMaster, ControlledMaster>();
        var control = (ControlledMaster)(object)native;
        var old = new ModbusConnectionResource(native, new NoopTransport(), 3000);
        var fresh = new ModbusConnectionResource(DispatchProxy.Create<IModbusMaster, ControlledMaster>(), new NoopTransport(), 3000);
        IStepPlugin plugin = batch ? new ModbusBatchReadPlugin() : new ModbusReadPlugin();
        object setting = batch
            ? new ModbusBatchReadSetting { ConnectionName = "\"C\"", Items = [new ModbusBatchItem { ResultVariable = "Locals.rx" }] }
            : new ModbusReadSetting { ConnectionName = "\"C\"", StartAddress = "\"0\"", Quantity = "\"1\"", ResultVariable = "Locals.rx" };
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        string key = ModbusHelper.GetConnectionKey("C");
        state.Store.Values[key] = old;
        var continuation = new HeldContinuationContext();
        var execution = continuation.Start(() => plugin.CreateExecutor().ExecuteAsync(context, caller.Token));
        try
        {
            await continuation.PumpUntilAsync(control.Entered.Task, execution, guard.Token);
            control.Result.SetResult([77]);
            await continuation.WaitForContinuationAsync(guard.Token);
            Assert.False(execution.IsCompleted);
            if (cancel) caller.Cancel(); else state.Store.Values[key] = fresh;
            await continuation.DrainUntilAsync(execution, guard.Token);
            var result = await execution;
            Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            Assert.Empty(state.Published);
            if (!cancel) Assert.Same(fresh, state.Store.Values[key]);
            Assert.False(fresh.IsClosed);
        }
        finally
        {
            control.Result.TrySetResult([77]);
            await continuation.DrainUntilAsync(execution, guard.Token);
            await old.CloseAsync(1000, default);
            await fresh.CloseAsync(1000, default);
        }
    }

    public class ControlledMaster : DispatchProxy
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<ushort[]> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "ReadHoldingRegistersAsync") { Entered.TrySetResult(); return Result.Task; }
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    private sealed class NoopTransport : IDisposable { public void Dispose() { } }
}
