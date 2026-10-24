using AuditTestSupport;
using Modbus.Helpers;
using NModbus;
using System.Reflection;
using Xunit;
namespace Modbus.ContractTests;
public class OpeningOwnershipTests
{
    [Fact]
    public async Task DisconnectCanFreezeAnInProgressOpenAndRejectLatePublication()
    {
        var context = ContextProxy.Create(); var owner = new ModbusConnectionResource(1000); var transport = new Transport();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opening = ModbusLifecycle.OpenRegisteredAsync(context, "M", owner, async (resource, ct) =>
        {
            resource.OwnTransport(transport); resource.OwnMaster(DispatchProxy.Create<IModbusMaster, ModbusLifecycleTests.MasterProxy>());
            entered.TrySetResult(); await exit.Task;
        }, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(context.Resources.TryGet<ModbusConnectionResource>(ModbusHelper.GetConnectionKey("M"), out var registered));
            var closing = registered.CloseAsync(1000, default);
            Assert.False(transport.Disposed);
            exit.TrySetResult(); await closing;
            await Assert.ThrowsAnyAsync<Exception>(() => opening);
            Assert.False(context.Resources.TryGet<ModbusConnectionResource>(ModbusHelper.GetConnectionKey("M"), out _));
        }
        finally { exit.TrySetResult(); await opening.ContinueWith(task => { _ = task.Exception; }); }
    }
    [Fact]
    public async Task FailedOpenCleanupRemainsRegisteredUntilExplicitCloseSucceeds()
    {
        var context = ContextProxy.Create(); var owner = new ModbusConnectionResource(1000); var transport = new Transport { Fail = true };
        await Assert.ThrowsAnyAsync<Exception>(() => ModbusLifecycle.OpenRegisteredAsync(context, "M", owner, (resource, ct) =>
        {
            resource.OwnTransport(transport); throw new InvalidOperationException("open");
        }, default));
        Assert.True(context.Resources.TryGet<ModbusConnectionResource>(ModbusHelper.GetConnectionKey("M"), out var registered));
        Assert.Same(owner, registered);
        transport.Fail = false; await owner.CloseAsync(1000, default);
    }
    private sealed class Transport : IDisposable
    {
        public bool Fail, Disposed;
        public void Dispose() { if (Fail) throw new InvalidOperationException("close"); Disposed = true; }
    }
}
