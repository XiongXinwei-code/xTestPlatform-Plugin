using System.IO;
using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class NativeWriteStopTests
{
    [Fact]
    public async Task Historical_failure_does_not_keep_a_finished_job_name_occupied()
    {
        var owner = new CanConnectionResource(new Driver());
        var job = owner.StartJob("A", _ => Task.FromException(new IOException("earlier cyclic failure")));
        try
        {
            await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            await owner.StopJobAsync("A", 1000, default);
            owner.StartJob("A", _ => Task.CompletedTask);
            await owner.StopJobAsync("A", 1000, default);
            Assert.NotNull(job.Error);
        }
        finally { try { await owner.CloseAsync(2000); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task Soft_timeout_does_not_make_an_owned_native_write_disappear()
    {
        var raw = new Driver();
        var owner = new CanConnectionResource(raw);
        var job = owner.StartJob("A", token => owner.WriteAsync(new() { Id = 0x201, Data = [1] }, 50, token));
        Task? stopping = null;
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            using var guard = new CancellationTokenSource(5000);
            while (job.Error == null) await Task.Delay(1, guard.Token);
            stopping = owner.StopJobAsync("A", 2000, default);
            Assert.NotSame(stopping, await Task.WhenAny(stopping, Task.Delay(40)));
            Assert.False(job.Completion.IsCompleted);
            raw.Exit.Set();
            await stopping;
            Assert.True(job.Completion.IsCompletedSuccessfully);
        }
        finally
        {
            raw.Exit.Set();
            if (stopping != null) try { await stopping; } catch (Exception) { }
            try { await owner.CloseAsync(2000); } catch (Exception) when (owner.IsClosed) { }
        }
    }

    [Fact]
    public async Task Stopping_finished_A_does_not_wait_for_Bs_native_write()
    {
        var raw = new Driver();
        var owner = new CanConnectionResource(raw);
        var a = owner.StartJob("A", _ => Task.FromException(new IOException("earlier A failure")));
        await a.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        var b = owner.StartJob("B", _ => Task.Run(() => owner.Write(new() { Id = 0x202, Data = [2] })));
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await owner.StopJobAsync("A", 500, default);
            Assert.False(b.Completion.IsCompleted);
            raw.Exit.Set();
            await owner.StopJobAsync("B", 2000, default);
        }
        finally { raw.Exit.Set(); try { await owner.CloseAsync(2000); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task Timed_out_managed_worker_stop_does_not_isolate_unrelated_jobs()
    {
        var owner = new CanConnectionResource(new Driver());
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.StartJob("A", _ => exit.Task);
        var b = owner.StartJob("B", token => Task.Delay(Timeout.Infinite, token));
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => owner.StopJobAsync("A", 30, default));
            Assert.Null(owner.IsolationReason);
            Assert.False(b.Completion.IsCompleted);
        }
        finally { exit.TrySetResult(); try { await owner.CloseAsync(2000); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task Already_cancelled_stop_does_not_cancel_or_isolate_a_running_job()
    {
        var owner = new CanConnectionResource(new Driver());
        var job = owner.StartJob("A", token => Task.Delay(Timeout.Infinite, token));
        using var caller = new CancellationTokenSource(); caller.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.StopJobAsync("A", 1000, caller.Token));
            Assert.False(job.Completion.IsCompleted);
            Assert.Null(owner.IsolationReason);
        }
        finally { await owner.CloseAsync(2000); }
    }

    private sealed class Driver : ICanAdapter
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Exit = new();
        public bool IsConnected => true;
        public void Open(CanAdapterConfig config) { }
        public void Close() { }
        public void Dispose() { }
        public void Write(CanMessage message) { Entered.TrySetResult(); if (!Exit.Wait(10000)) throw new TimeoutException("test write barrier"); }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
    }
}
