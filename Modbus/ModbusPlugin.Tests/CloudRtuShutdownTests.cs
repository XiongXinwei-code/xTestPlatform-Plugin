using System.Reflection;
using System.IO;
using Modbus.Helpers;
using NModbus;
using Xunit;

namespace Modbus.ContractTests;

[Collection("Modbus followup isolated")]
public sealed class CloudRtuShutdownTests
{
    [Fact]
    public async Task Rtu_abort_calls_serial_close_before_waiting_for_read_and_confirms_abort_before_final_dispose()
    {
        using var guard = new CancellationTokenSource(15000);
        using var ioExit = new ManualResetEventSlim();
        using var abortExit = new ManualResetEventSlim();
        var port = new Port(ioExit, abortExit, guard.Token);
        var adapter = new SerialPortAdapter(port);
        var owner = new ModbusConnectionResource(DispatchProxy.Create<IModbusMaster, ModbusLifecycleTests.MasterProxy>(), adapter, 30000);
        var reading = owner.WithTimeoutAsync(() => Task.FromResult(adapter.Read(new byte[1], 0, 1)), 30000, "RTU read", default);
        await port.Reading.Task.WaitAsync(guard.Token);
        var closing = owner.CloseAsync(5000, default);
        try
        {
            await port.CloseEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(new[] { "purge-out", "purge-in", "close" }, port.Calls.ToArray());
            Assert.False(owner.IsClosed); Assert.Equal(0, port.DisposeCalls);
            Assert.Throws<InvalidOperationException>(() => adapter.Read(new byte[1], 0, 1));
            Assert.Equal(1, port.ReadCalls); // NModbus must not start a retry while Close is still in progress.
            ioExit.Set(); await port.IoExited.Task.WaitAsync(guard.Token);
            Assert.False(closing.IsCompleted); Assert.Equal(0, port.DisposeCalls);
            abortExit.Set(); await closing.WaitAsync(guard.Token);
            Assert.True(owner.IsClosed); Assert.Equal(1, port.DisposeCalls);
            await Assert.ThrowsAsync<InvalidOperationException>(() => reading);
            Assert.Throws<InvalidOperationException>(() => adapter.Read(new byte[1], 0, 1));
            Assert.Equal(1, port.ReadCalls);
        }
        finally
        {
            ioExit.Set(); abortExit.Set();
            await owner.CloseAsync(5000, guard.Token);
            try { await reading; } catch (Exception) { }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_status_or_purge_failure_does_not_prevent_the_legal_sdk_close(bool statusFailure)
    {
        using var ioExit = new ManualResetEventSlim(true);
        using var abortExit = new ManualResetEventSlim(true);
        var port = new Port(ioExit, abortExit, default) { FailStatus = statusFailure, FailPurge = !statusFailure };
        var adapter = new SerialPortAdapter(port);
        var failure = Assert.Throws<AggregateException>(adapter.CancelPendingIo);
        Assert.Contains("fake SDK failure", failure.ToString());
        Assert.True(port.CloseEntered.Task.IsCompleted);
        Assert.Equal("close", port.Calls.Last());
        adapter.Dispose();
        Assert.Equal(1, port.DisposeCalls);
    }

    private sealed class Port(ManualResetEventSlim ioExit, ManualResetEventSlim abortExit, CancellationToken guard) : ISerialPortOperations
    {
        internal readonly TaskCompletionSource Reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CloseEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource IoExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly List<string> Calls = [];
        internal int ReadCalls, DisposeCalls;
        internal bool FailStatus, FailPurge;
        private bool _abortReturned;
        public bool IsOpen => FailStatus ? throw new IOException("fake SDK failure: status") : true;
        public int ReadTimeout { get; set; } = 30000;
        public int WriteTimeout { get; set; } = 30000;
        public int Read(byte[] buffer, int offset, int count)
        {
            Interlocked.Increment(ref ReadCalls); Reading.TrySetResult(); ioExit.Wait(guard); IoExited.TrySetResult();
            throw new IOException("read interrupted");
        }
        public void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public void DiscardInBuffer() => Calls.Add("purge-in");
        public void DiscardOutBuffer() { Calls.Add("purge-out"); if (FailPurge) throw new IOException("fake SDK failure: purge"); }
        public void Close() { Calls.Add("close"); CloseEntered.TrySetResult(); ioExit.Set(); abortExit.Wait(guard); _abortReturned = true; }
        public void Dispose()
        {
            if (Reading.Task.IsCompleted) Assert.True(IoExited.Task.IsCompleted);
            if (CloseEntered.Task.IsCompleted) Assert.True(_abortReturned);
            Interlocked.Increment(ref DisposeCalls);
        }
    }
}
