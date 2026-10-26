using PluginCommon;
using Xunit;

namespace Modbus.ContractTests;

[Collection("Modbus followup isolated")]
public sealed class OwnerStopMechanismTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_close_retires_active_and_queued_waiters_without_releasing_unfinished_native_io(bool automatic)
    {
        var gate = new PendingIoGate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nativeResult = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        int laterSubmissions = 0, releases = 0;
        var active = gate.RunAsync(async () => { entered.TrySetResult(); return await nativeResult.Task; }, 30000, "非合作原生操作", CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = gate.RunAsync(() => { Interlocked.Increment(ref laterSubmissions); return Task.FromResult(2); }, 30000, "排队中的旧步骤", CancellationToken.None);
        Task? explicitClose = null;
        try
        {
            if (automatic) gate.RequestClose(() => Interlocked.Increment(ref releases));
            else explicitClose = gate.CloseAsync(() => Interlocked.Increment(ref releases), 5000, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => active.WaitAsync(TimeSpan.FromMilliseconds(500)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => queued.WaitAsync(TimeSpan.FromMilliseconds(500)));
            Assert.True(gate.HasPendingOperation);
            Assert.False(gate.IsClosed);
            Assert.False(gate.CloseCompletion!.IsCompleted);
            Assert.Equal(0, releases);
            Assert.Equal(0, laterSubmissions);
            await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync(() => Task.FromResult(3), 100, "禁止新提交", CancellationToken.None));
        }
        finally
        {
            nativeResult.TrySetResult(1);
            await Ignore(active); await Ignore(queued);
            if (explicitClose != null) await explicitClose;
            await gate.CloseCompletion!.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(gate.IsClosed);
        Assert.False(gate.HasPendingOperation);
        Assert.Equal(1, releases);
        Assert.Equal(0, laterSubmissions);
    }

    [Fact]
    public async Task Synchronously_failed_close_never_runs_diagnostics_on_the_resource_cleanup_thread()
    {
        var gate = new PendingIoGate(_ => Task.FromException(new InvalidOperationException("fake immediate close failure")));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseReport = new ManualResetEventSlim();
        int callerThread = 0, insideRequest = 0, inlineReport = 0;
        var request = Task.Run(() =>
        {
            callerThread = Environment.CurrentManagedThreadId;
            Volatile.Write(ref insideRequest, 1);
            gate.RequestClose(() => { }, _ =>
            {
                if (Environment.CurrentManagedThreadId == callerThread && Volatile.Read(ref insideRequest) == 1)
                    Interlocked.Exchange(ref inlineReport, 1);
                entered.TrySetResult();
                if (!releaseReport.Wait(10000)) throw new TimeoutException("test diagnostic barrier");
            });
            Volatile.Write(ref insideRequest, 0);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await request.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.Equal(0, inlineReport); // A pooled thread may be reused after RequestClose has returned.
            Assert.False(gate.IsClosed);
            await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync(() => Task.FromResult(1), 100, "已停止准入", CancellationToken.None));
        }
        finally { releaseReport.Set(); await request.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public async Task Original_user_cancellation_still_does_not_release_the_native_operation()
    {
        var gate = new PendingIoGate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caller = new CancellationTokenSource();
        var operation = gate.RunAsync(async () => { entered.TrySetResult(); return await release.Task; }, 30000, "原调用方取消", caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.True(gate.HasPendingOperation); Assert.False(gate.IsClosed);
        release.TrySetResult(1);
        await gate.CloseAsync(() => { }, 2000, CancellationToken.None);
        Assert.True(gate.IsClosed);
    }

    private static async Task Ignore(Task task) { try { await task; } catch (InvalidOperationException) { } }

    [Fact]
    public async Task Stop_racing_semaphore_admission_never_leaks_or_duplicates_a_permit()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            using var gate = new SemaphoreSlim(0, 1);
            var stop = new OwnerStopSignal();
            var admission = stop.EnterAsync(gate, Timeout.Infinite, default, "准入竞态");
            await Task.WhenAll(Task.Run(stop.RequestStop), Task.Run(() => gate.Release()));
            try { if (await admission) gate.Release(); }
            catch (InvalidOperationException) { }
            Assert.Equal(1, gate.CurrentCount);
        }
    }

    [Fact]
    public async Task Background_owner_exit_cannot_be_faked_by_retiring_its_foreground_observers()
    {
        var gate = new PendingIoGate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        int released = 0;
        var worker = gate.RunAsync(async () => { entered.TrySetResult(); return await native.Task; },
            Timeout.Infinite, "后台持有者", default, retireWaitOnClose: false);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        gate.RequestClose(() => Interlocked.Increment(ref released));
        Assert.False(worker.IsCompleted); Assert.False(gate.IsClosed); Assert.Equal(0, released);
        native.SetResult(7);
        Assert.Equal(7, await worker.WaitAsync(TimeSpan.FromSeconds(5)));
        await gate.CloseCompletion!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, released); Assert.True(gate.IsClosed);
    }
}
