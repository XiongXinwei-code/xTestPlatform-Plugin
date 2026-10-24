using Modbus.Helpers;
using System.IO;
using Modbus.Models;
using NModbus;
using System.Reflection;
using Xunit;

namespace Modbus.ContractTests;

public sealed class ImmediateRerunTests
{
    [Fact]
    public async Task NewExecutionWaitsForRetiringPhysicalEndpointBeforeConnect()
    {
        string physical = ModbusHelper.GetPhysicalResourceKey(ModbusTransportType.TCP, "127.0.0.1", Random.Shared.Next(20000, 30000));
        var transport = new Transport();
        var old = new ModbusConnectionResource(DispatchProxy.Create<IModbusMaster, ModbusLifecycleTests.MasterProxy>(), transport, 2000, physicalResourceKey: physical);
        var next = new ModbusConnectionResource(2000, physicalResourceKey: physical);
        var raw = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        var pending = old.WithTimeoutAsync(() => { entered.TrySetResult(); return raw.Task; }, 2000, "old read", cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); old.Dispose();
        bool opened = false;
        var opening = next.InitializeAsync((owner, ct) =>
        {
            if (!transport.Released) throw new IOException("physical endpoint still retiring");
            opened = true; owner.OwnTransport(new Transport()); owner.OwnMaster(DispatchProxy.Create<IModbusMaster, ModbusLifecycleTests.MasterProxy>());
            return Task.CompletedTask;
        }, default);
        try
        {
            await Task.WhenAny(opening, Task.Delay(100)); Assert.False(opening.IsCompleted);
            raw.TrySetResult(); await opening; Assert.True(opened);
        }
        finally { raw.TrySetResult(); await old.CloseAsync(2000, default); await next.CloseAsync(2000, default); }
    }

    private sealed class Transport : IDisposable { internal bool Released; public void Dispose() => Released = true; }
}
