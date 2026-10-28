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
            await Assert.ThrowsAsync<InvalidOperationException>(() => opening.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Same(owner, registered); Assert.False(owner.IsClosed);
            exit.TrySetResult(); await closing;
            // The retired foreground no longer waits just to remove a registry key.
            // A closed exact-owner tombstone may remain until Disconnect/next Connect.
            Assert.True(owner.IsClosed); Assert.True(transport.Disposed);
            var next = new ModbusConnectionResource(1000);
            await ModbusLifecycle.OpenRegisteredAsync(context, "M", next, (resource, _) =>
            {
                resource.OwnTransport(new Transport()); resource.OwnMaster(DispatchProxy.Create<IModbusMaster, ModbusLifecycleTests.MasterProxy>());
                return Task.CompletedTask;
            }, default);
            Assert.True(context.Resources.TryGet<ModbusConnectionResource>(ModbusHelper.GetConnectionKey("M"), out var current));
            Assert.Same(next, current);
            await next.CloseAsync(1000, default);
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
