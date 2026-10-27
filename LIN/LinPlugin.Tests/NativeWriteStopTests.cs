using System.IO;
using LIN.Adapters;
using LIN.Models;
using LIN.Runtime;
using Xunit;

namespace LIN.Tests;

[Collection("LIN managed retirement")]
public sealed class NativeWriteStopTests
{
    [Fact]
    public async Task Historical_failure_does_not_fail_stop_after_all_owned_writes_exited()
    {
        var owner = new LinConnectionResource(new Driver());
        var job = owner.StartJob([_ => Task.FromException(new IOException("earlier cyclic write failure"))]);
        try
        {
            await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            await job.StopAsync(1000);
            Assert.NotNull(job.Error);
        }
        finally { await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task Soft_timeout_does_not_make_an_owned_native_write_disappear()
    {
        var raw = new Driver { BlockWrite = true };
        var owner = new LinConnectionResource(raw);
        var job = owner.StartJob([token => Task.Run(() => owner.Write(new() { Data = [1] }, token, 50))]);
        Task? stopping = null;
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await WaitForError(job);
            stopping = job.StopAsync(2000);
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
            await owner.CloseAsync(2000);
        }
    }

    [Fact]
    public async Task Invalid_stop_does_not_cancel_a_running_job()
    {
        var owner = new LinConnectionResource(new Driver());
        var job = owner.StartJob([token => Task.Delay(Timeout.Infinite, token)]);
        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => job.StopAsync(0));
            Assert.False(job.Completion.IsCompleted);
        }
        finally { await job.StopAsync(1000); await owner.CloseAsync(2000); }
    }

    private static async Task WaitForError(LinCyclicJob job)
    {
        using var guard = new CancellationTokenSource(5000);
        while (job.Error == null) await Task.Delay(1, guard.Token);
    }

    [Fact]
    public async Task Queued_actual_after_stop_cannot_enter_the_sdk()
    {
        var raw = new Driver();
        var owner = new LinConnectionResource(raw);
        var beforeProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseProvider = new ManualResetEventSlim();
        int checks = 0;
        var job = owner.StartJob([_ => Task.Run(() => owner.Write(new() { Data = [1] }, default, 50,
            checkExecution: () =>
            {
                if (Interlocked.Increment(ref checks) == 2)
                {
                    beforeProvider.TrySetResult();
                    if (!releaseProvider.Wait(10000)) throw new TimeoutException("test admission barrier");
                }
            }))]);
        try
        {
            await beforeProvider.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await WaitForError(job);
            await job.StopAsync(1000);
            releaseProvider.Set();
            await owner.CloseAsync(2000);
            Assert.False(raw.Entered.Task.IsCompleted);
        }
        finally { releaseProvider.Set(); await owner.CloseAsync(2000); }
    }

    private sealed class Driver : ILinAdapter
    {
        internal bool BlockWrite;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Exit = new();
        public bool IsConnected => true;
        public void Open(LinAdapterConfig config) { }
        public void Close() { }
        public void Dispose() { }
        public void Write(LinFrame frame) { Entered.TrySetResult(); if (BlockWrite && !Exit.Wait(10000)) throw new TimeoutException("test write barrier"); }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => null;
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => null;
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { }
    }
}
