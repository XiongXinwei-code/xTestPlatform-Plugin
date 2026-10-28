using SerialPort.Helpers;
using System.IO;
using System.Reflection;
using Xunit;

namespace SerialPort.Tests;

public sealed class CloudSerialShutdownTests
{
    [Fact]
    public async Task Successful_dispose_cannot_restart_failed_interrupt_before_closed_state_is_published()
    {
        using var guard = new CancellationTokenSource(15000);
        using var disposeExit = new ManualResetEventSlim();
        var channel = new DisposeBarrierChannel(disposeExit, guard.Token);
        var owner = new SerialConnectionResource(channel);
        var closing = owner.CloseAsync(10000, default);
        Task? repeated = null;
        try
        {
            await channel.DisposeEntered.Task.WaitAsync(guard.Token);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var ownerType = typeof(SerialConnectionResource);
            var shutdownField = ownerType.GetField("_shutdown", flags)!;
            var releasingField = ownerType.GetField("_releasing", flags)!;
            var io = ownerType.GetField("_io", flags)!.GetValue(owner)!;
            var gateState = io.GetType().GetField("_state", flags)!.GetValue(io)!;
            var originalShutdown = shutdownField.GetValue(owner);

            // Hold only the shared closed-state publication lock, after Dispose has
            // entered. The test thread can reenter it to issue another public Close.
            // This exposes the real gap between the release callback returning and
            // PendingIoGate publishing _closed without changing production scheduling.
            await Task.Run(() =>
            {
                lock (gateState)
                {
                    disposeExit.Set();
                    Assert.True(SpinWait.SpinUntil(() => !(bool)releasingField.GetValue(owner)!, 5000));
                    repeated = owner.CloseAsync(10000, default);
                    Assert.Same(originalShutdown, shutdownField.GetValue(owner));
                }
            }, guard.Token);
            await closing;
            await repeated!;
            Assert.NotNull(owner.CloseWarning);
            Assert.True(owner.IsClosed);
            Assert.Equal(1, channel.InterruptCalls);
            Assert.Equal(1, channel.DisposeCalls);
        }
        finally
        {
            disposeExit.Set();
            try { await closing.WaitAsync(guard.Token); } catch (Exception) { }
            if (repeated is not null) { try { await repeated.WaitAsync(guard.Token); } catch (Exception) { } }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Infinite_write_is_interrupted_before_final_dispose_and_both_real_exits_are_confirmed(bool interruptFails)
    {
        using var guard = new CancellationTokenSource(15000);
        using var ioExit = new ManualResetEventSlim();
        using var abortExit = new ManualResetEventSlim();
        var channel = new Channel(ioExit, abortExit, guard.Token, interruptFails);
        var owner = new SerialConnectionResource(channel);
        var writing = owner.WriteAsync([0x55], default);
        await channel.Writing.Task.WaitAsync(guard.Token);
        var closing = owner.CloseAsync(5000, default);
        try
        {
            await channel.InterruptEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(1, channel.InterruptCalls);
            Assert.False(owner.IsClosed);
            Assert.Equal(0, channel.DisposeCalls);
            ioExit.Set();
            await channel.IoExited.Task.WaitAsync(guard.Token);
            Assert.False(closing.IsCompleted);
            Assert.Equal(0, channel.DisposeCalls);
            abortExit.Set();
            await closing.WaitAsync(guard.Token);
            Assert.Equal(interruptFails, owner.CloseWarning != null);
            Assert.True(owner.IsClosed);
            Assert.Equal(1, channel.DisposeCalls);
            await Assert.ThrowsAsync<InvalidOperationException>(() => writing);
        }
        finally
        {
            ioExit.Set(); abortExit.Set();
            try { await owner.CloseAsync(5000, guard.Token); } catch (InvalidOperationException) when (interruptFails && owner.IsClosed) { }
            try { await writing; } catch (Exception) { }
        }
    }

    private sealed class Channel(ManualResetEventSlim ioExit, ManualResetEventSlim abortExit, CancellationToken guard, bool interruptFails)
        : ISerialChannel, IInterruptibleSerialChannel
    {
        internal readonly TaskCompletionSource Writing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource InterruptEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource IoExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int InterruptCalls, DisposeCalls;
        private bool _abortReturned;
        public bool IsOpen => DisposeCalls == 0;
        public int ReadTimeout { get; set; } = -1;
        public int WriteTimeout { get; set; } = -1;
        public int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public void Write(byte[] bytes)
        {
            Writing.TrySetResult(); ioExit.Wait(guard); IoExited.TrySetResult();
            throw new IOException("write was interrupted; already transmitted byte count is unknown");
        }
        public void CancelPendingIo()
        {
            Interlocked.Increment(ref InterruptCalls); InterruptEntered.TrySetResult(); ioExit.Set(); abortExit.Wait(guard); _abortReturned = true;
            if (interruptFails) throw new IOException("interrupt failed after its native call exited");
        }
        public void Dispose()
        {
            Assert.True(IoExited.Task.IsCompleted);
            if (InterruptEntered.Task.IsCompleted) Assert.True(_abortReturned);
            Interlocked.Increment(ref DisposeCalls);
        }
    }

    private sealed class DisposeBarrierChannel(ManualResetEventSlim disposeExit, CancellationToken guard)
        : ISerialChannel, IInterruptibleSerialChannel
    {
        internal readonly TaskCompletionSource DisposeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int InterruptCalls, DisposeCalls;
        public bool IsOpen => DisposeCalls == 0;
        public int ReadTimeout { get; set; } = -1;
        public int WriteTimeout { get; set; } = -1;
        public int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public void Write(byte[] bytes) => throw new NotSupportedException();
        public void CancelPendingIo()
        {
            Interlocked.Increment(ref InterruptCalls);
            throw new IOException("interrupt failed, but its native call has exited");
        }
        public void Dispose()
        {
            Interlocked.Increment(ref DisposeCalls);
            DisposeEntered.TrySetResult();
            disposeExit.Wait(guard);
        }
    }
}
