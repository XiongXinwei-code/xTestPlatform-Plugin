using System.IO;
using System.Reflection;
using Modbus.Helpers;
using NModbus;
using Xunit;

namespace Modbus.ContractTests;

[Collection("Modbus followup isolated")]
public sealed class CloseOutcomeTests
{
    [Fact]
    public async Task Completed_interrupt_failure_does_not_fail_successful_dispose()
    {
        var transport = new Transport();
        var owner = new ModbusConnectionResource(
            DispatchProxy.Create<IModbusMaster, ModbusLifecycleTests.MasterProxy>(), transport, 2000);

        await owner.CloseAsync(2000, default);
        await owner.CloseAsync(2000, default);

        Assert.True(owner.IsClosed);
        Assert.Equal(1, transport.Interrupts);
        Assert.Equal(1, transport.Disposes);
    }

    private sealed class Transport : IDisposable, ICancellableModbusTransport
    {
        internal int Interrupts, Disposes;
        public void CancelPendingIo() { Interlocked.Increment(ref Interrupts); throw new IOException("interrupt returned failure"); }
        public void Dispose() => Interlocked.Increment(ref Disposes);
    }
}
