using LIN.Adapters;
using LIN.Models;
using LIN.Runtime;
using Xunit;

namespace LIN.Tests;

[CollectionDefinition("LIN owner retirement", DisableParallelization = true)]
public sealed class OwnerRetirementCollection { }

[Collection("LIN owner retirement")]
public sealed class OwnerRetirementMechanismTests
{
    [Theory]
    [InlineData("Write")]
    [InlineData("Wakeup")]
    [InlineData("Sleep")]
    public async Task Close_retires_front_wait_before_noncooperative_call_exits(string operation)
    {
        var raw = new Driver(operation);
        var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = Guid.NewGuid().ToString() });
        var waiting = operation switch
        {
            "Write" => Task.Run(() => owner.Write(new() { Data = [1] }, default, 60000, cancelWait: true)),
            "Wakeup" => Task.Run(() => owner.Wakeup(true, 60000, default)),
            _ => owner.SleepAsync(true, 60000, default)
        };
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner.Dispose();
            Assert.Same(waiting, await Task.WhenAny(waiting, Task.Delay(400)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.Release.TrySetResult();
            try { await waiting; } catch (Exception) { }
            await owner.CloseAsync();
        }
    }

    [Fact]
    public async Task Stop_retires_queued_transaction_even_when_native_cancel_callback_blocks()
    {
        var raw = new Driver("Read") { BlockCancellationCallback = true };
        var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = Guid.NewGuid().ToString() });
        var reading = Task.Run(() => owner.Read(60000));
        await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var queued = Task.Run(() => owner.Write(new() { Data = [1] }, default, 60000, cancelWait: true));
        try
        {
            owner.Dispose();
            await raw.CancelEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Same(reading, await Task.WhenAny(reading, Task.Delay(400)));
            Assert.Same(queued, await Task.WhenAny(queued, Task.Delay(400)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => reading);
            await Assert.ThrowsAsync<InvalidOperationException>(() => queued);
            Assert.Equal(0, raw.Writes); Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.ReleaseCancellation.TrySetResult(); raw.Release.TrySetResult();
            try { await reading; } catch (Exception) { }
            try { await queued; } catch (Exception) { }
            await owner.CloseAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_cancel_does_not_run_driver_callbacks_inline_or_release_native_ownership_early(bool failBoth)
    {
        var raw = new Driver("Read") { BlockCancellationCallback = true, ReturnBeforeCallbackExit = true, FailBoth = failBoth };
        var owner = new LinConnectionResource(raw); owner.Open(new() { Channel = Guid.NewGuid().ToString() });
        using var caller = new CancellationTokenSource();
        var reading = Task.Run(() => owner.Read(60000, caller.Token));
        Task? cancelling = null;
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancelling = Task.Run(caller.Cancel);
            await raw.CancelEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Same(cancelling, await Task.WhenAny(cancelling, Task.Delay(400)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(1)));
            owner.Dispose(); raw.Release.TrySetResult();
            await Task.Delay(50);
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.Release.TrySetResult(); raw.ReleaseCancellation.TrySetResult();
            if (cancelling != null) await cancelling;
            try { await reading; } catch (Exception) { }
            if (!failBoth) await owner.CloseAsync();
            else
            {
                var error = await Assert.ThrowsAsync<AggregateException>(() => owner.CloseAsync());
                Assert.Contains("native failure", error.ToString()); Assert.Contains("callback failure", error.ToString());
                Assert.True(owner.IsClosed);
            }
        }
    }

    [Fact]
    public async Task Closing_keeps_cyclic_job_joined_until_its_native_write_really_exits()
    {
        var raw = new Driver("Write");
        var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = Guid.NewGuid().ToString() });
        var job = owner.StartJob([token => Task.Run(() => owner.Write(new() { Data = [1] }, token, 60000))]);
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner.Dispose();
            await Task.Delay(80);
            Assert.False(job.Completion.IsCompleted);
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.Release.TrySetResult();
            try { await owner.CloseAsync(); } catch (AggregateException) when (owner.IsClosed) { }
        }
        await job.Completion;
        Assert.True(owner.IsClosed);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class Driver(string blocked) : ILinAdapter
    {
        internal readonly TaskCompletionSource Entered = Signal(), Release = Signal(), CancelEntered = Signal(), ReleaseCancellation = Signal();
        internal int Closes, Writes;
        internal bool BlockCancellationCallback, ReturnBeforeCallbackExit, FailBoth;
        public bool IsConnected { get; private set; }
        public void Open(LinAdapterConfig config) => IsConnected = true;
        public void Close() { Interlocked.Increment(ref Closes); IsConnected = false; }
        public void Dispose() => Close();
        public void Write(LinFrame frame) { Interlocked.Increment(ref Writes); Block("Write"); }
        public void Wakeup(bool remote = true) => Block("Wakeup");
        public void Sleep(bool remote = true) => Block("Sleep");
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default)
        {
            if (ReturnBeforeCallbackExit)
            {
                _ = ct.Register(() =>
                {
                    CancelEntered.TrySetResult(); ReleaseCancellation.Task.GetAwaiter().GetResult();
                    if (FailBoth) throw new InvalidOperationException("LIN callback failure");
                });
                Block("Read");
                if (FailBoth) throw new InvalidOperationException("LIN native failure");
                return null;
            }
            using var registered = BlockCancellationCallback ? ct.Register(() =>
            {
                CancelEntered.TrySetResult(); ReleaseCancellation.Task.GetAwaiter().GetResult();
            }) : default;
            Block("Read"); return null;
        }
        public LinFrame? Read(byte frameId, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
        private void Block(string operation)
        {
            if (blocked != operation) return;
            Entered.TrySetResult(); Release.Task.GetAwaiter().GetResult();
        }
    }
}
