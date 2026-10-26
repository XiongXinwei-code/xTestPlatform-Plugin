using System.Reflection;
using System.Net;
using System.Net.Sockets;
using Modbus.Helpers;
using NModbus;
using Xunit;

namespace Modbus.ContractTests;

[Collection("Modbus followup isolated")]
public class ModbusLifecycleTests
{
    [Fact]
    public async Task ClosingTcpUnblocksAnActualAsynchronousSocketReadBeforeDisposal()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        using var server = await listener.AcceptTcpClientAsync();
        var stream = new CancellableTcpStream(client, 1000);
        var resource = new ModbusConnectionResource(DispatchProxy.Create<IModbusMaster, MasterProxy>(), stream, 1000);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => resource.WithTimeoutAsync(() => Task.FromResult(stream.Read(new byte[1], 0, 1)), 50, "silent server", default));
            await resource.CloseAsync(500, default);
        }
        finally { server.Dispose(); listener.Stop(); await resource.CloseAsync(1000, default); }
    }
    [Fact]
    public async Task CancelledOutputKeepsOwnershipUntilRawCallExits()
    {
        var master = DispatchProxy.Create<IModbusMaster, MasterProxy>();
        var transport = new TrackedTransport();
        var resource = new ModbusConnectionResource(master, transport, 1000);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var operation = resource.WithTimeoutAsync(() => { started.SetResult(); return raw.Task; }, 1000, "output", cts.Token);
        await started.Task;
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        var submitted = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => resource.WithTimeoutAsync(() => { submitted = true; return Task.CompletedTask; }, 1000, "next", default));
        Assert.False(submitted);
        var closing = resource.CloseAsync(1000, default);
        Assert.False(transport.Disposed);
        raw.SetResult();
        await closing;
        Assert.True(transport.Disposed);
    }

    [Fact]
    public async Task AlreadyCancelledTokenCannotSubmitOutput()
    {
        var resource = new ModbusConnectionResource(DispatchProxy.Create<IModbusMaster, MasterProxy>(), new TrackedTransport(), 1000);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var submitted = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resource.WithTimeoutAsync(() => { submitted = true; return Task.CompletedTask; }, 1000, "output", cts.Token));
        Assert.False(submitted);
        await resource.CloseAsync(1000, default);
    }

    [Fact]
    public async Task CloseFreezesTransportEvenWhenPendingPublicationHasNotHappened()
    {
        var transport = new CancellableTransport();
        var resource = new ModbusConnectionResource(DispatchProxy.Create<IModbusMaster, MasterProxy>(), transport, 1000);
        await resource.CloseAsync(1000, default);
        Assert.Equal(1, transport.Cancellations);
    }

    public class MasterProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }
    private sealed class TrackedTransport : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
    private sealed class CancellableTransport : IDisposable, ICancellableModbusTransport
    {
        public int Cancellations;
        public void CancelPendingIo() => Interlocked.Increment(ref Cancellations);
        public void Dispose() { }
    }
}
