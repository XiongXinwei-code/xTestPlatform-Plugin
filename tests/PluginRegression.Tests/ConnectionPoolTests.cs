extern alias EthernetCore;
using Ethernet.Runtime;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ConnectionPoolTests
{
    [Fact]
    public async Task Factory_result_cannot_publish_when_original_caller_cancelled_but_linked_callback_is_delayed()
    {
        string name = "cancel-boundary-" + Guid.NewGuid().ToString("N");
        using var pool = new NamedConnectionPool<Handle>();
        using var caller = new CancellationTokenSource();
        using var releaseCancel = new ManualResetEventSlim();
        var cancelEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exit = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new Handle();
        var opening = pool.OpenAsync(name, _ => { entered.TrySetResult(); return exit.Task; }, 5000, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var registration = caller.Token.Register(() => { cancelEntered.TrySetResult(); releaseCancel.Wait(10000); });
        var cancelling = Task.Run(caller.Cancel);
        try
        {
            await cancelEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            exit.TrySetResult(handle);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
            Assert.Throws<InvalidOperationException>(() => pool.Get(name));
        }
        finally { releaseCancel.Set(); exit.TrySetResult(handle); await cancelling; await pool.CloseAsync(name, 1000, default); }
    }

    [Fact]
    public async Task Delayed_retirement_retry_cannot_close_a_later_active_generation()
    {
        string name = Guid.NewGuid().ToString(); using var pool = new NamedConnectionPool<Handle>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var exit = new ManualResetEventSlim();
        var old = new Handle { BeforeRelease = () => { entered.TrySetResult(); exit.Wait(); } };
        await pool.OpenAsync(name, _ => Task.FromResult(old), 1000, default);
        var closing = pool.CloseAsync(name, 2000, default); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        object captured;
        try
        {
            // Capture exactly the retirement entry a concurrent waiter can have already read.
            var type = typeof(EthernetCore::PluginCommon.RetiringResources);
            var sync = type.GetField("Sync", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
            lock (sync)
            {
                var entries = (System.Collections.IDictionary)type.GetField("Entries", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
                var list = (System.Collections.IList)entries[$"Ethernet:{typeof(Handle).FullName}:{name}"]!;
                captured = list[0]!;
            }
        }
        finally { old.BeforeRelease = null; exit.Set(); }
        await closing;
        var current = new Handle(); await pool.OpenAsync(name, _ => Task.FromResult(current), 1000, default);
        var retry = (Task)captured.GetType().GetMethod("Retry", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(captured, [1000])!;
        await retry;
        Assert.Equal(0, current.Releases); Assert.Same(current, pool.Get(name));
        await pool.CloseAsync(name, 1000, default);
    }

    [Fact]
    public async Task New_execution_waits_for_old_execution_cleanup_and_can_retry_failed_dispose()
    {
        string name = Guid.NewGuid().ToString();
        var old = new NamedConnectionPool<Handle>(); var handle = new Handle { FailRelease = true };
        await old.OpenAsync(name, _ => Task.FromResult(handle), 1000, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => old.CloseAsync(name, 1000, default));
        using var next = new NamedConnectionPool<Handle>(); bool factoryCalled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => next.OpenAsync(name, _ =>
        { factoryCalled = true; return Task.FromResult(new Handle()); }, 1000, default));
        Assert.False(factoryCalled);
        handle.FailRelease = false;
        await next.OpenAsync(name, _ => { factoryCalled = true; return Task.FromResult(new Handle()); }, 1000, default);
        Assert.True(factoryCalled); old.Dispose(); await next.CloseAsync(name, 1000, default);
    }

    [Fact]
    public async Task Failed_late_staged_release_is_retained_for_close_retry()
    {
        string name = Guid.NewGuid().ToString(); using var pool = new NamedConnectionPool<Handle>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exit = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new Handle { FailRelease = true };
        var opening = pool.OpenAsync(name, _ => { entered.SetResult(); return exit.Task; }, 1000, default);
        await entered.Task;
        var closing = pool.CloseAsync(name, 1000, default); exit.SetResult(handle);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await opening);
        await Assert.ThrowsAsync<InvalidOperationException>(() => closing);
        handle.FailRelease = false;
        await pool.CloseAsync(name, 1000, default);
        Assert.True(handle.Releases >= 3);
    }

    [Fact]
    public async Task Close_retains_noncooperative_open_until_actual_exit_and_rejects_late_publication()
    {
        using var pool = new NamedConnectionPool<Handle>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exit = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new Handle();
        var opening = pool.OpenAsync("same", _ => { entered.SetResult(); return exit.Task; }, 1000, default);
        await entered.Task;
        await Assert.ThrowsAsync<TimeoutException>(() => pool.CloseAsync("same", 30, default));
        Assert.Throws<InvalidOperationException>(() => pool.Get("same"));
        Assert.Equal(0, handle.Releases);
        exit.SetResult(handle);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await opening);
        await pool.CloseAsync("same", 1000, default);
        Assert.Equal(1, handle.Releases);
    }

    [Fact]
    public async Task Superseded_open_cannot_overwrite_or_close_new_generation()
    {
        using var pool = new NamedConnectionPool<Handle>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exit = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new Handle(); var current = new Handle();
        var first = pool.OpenAsync("same", _ => { entered.SetResult(); return exit.Task; }, 1000, default);
        await entered.Task;
        var second = pool.OpenAsync("same", _ => Task.FromResult(current), 1000, default);
        exit.SetResult(old);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        Assert.Same(current, await second);
        Assert.Same(current, pool.Get("same"));
        Assert.Equal(1, old.Releases);
        Assert.Equal(0, current.Releases);
        await pool.CloseAsync("same", 1000, default);
    }

    [Fact]
    public async Task Failed_close_retains_resource_for_retry()
    {
        using var pool = new NamedConnectionPool<Handle>();
        var handle = new Handle { FailRelease = true };
        await pool.OpenAsync("same", _ => Task.FromResult(handle), 1000, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pool.CloseAsync("same", 1000, default));
        Assert.Throws<InvalidOperationException>(() => pool.Get("same"));
        handle.FailRelease = false;
        await pool.CloseAsync("same", 1000, default);
        Assert.Equal(2, handle.Releases);
    }

    [Fact]
    public async Task Already_cancelled_open_never_calls_factory()
    {
        using var pool = new NamedConnectionPool<Handle>();
        using var source = new CancellationTokenSource(); source.Cancel();
        var called = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pool.OpenAsync("same", _ =>
        { called = true; return Task.FromResult(new Handle()); }, 1000, source.Token));
        Assert.False(called);
    }

    private sealed class Handle : IDisposable
    {
        internal int Releases;
        internal bool FailRelease;
        internal Action? BeforeRelease;
        public void Dispose()
        {
            Interlocked.Increment(ref Releases);
            BeforeRelease?.Invoke();
            if (FailRelease) throw new InvalidOperationException("release failed");
        }
    }
}
