using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class RestartLifecycleTests
{
    [Fact]
    public async Task Dispose_then_immediate_open_waits_for_old_actual_close()
    {
        string key = Guid.NewGuid().ToString(); var raw = new Driver { BlockClose = true };
        var old = new CanConnectionResource(raw, key); old.Dispose();
        await raw.CloseEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var reopening = CanConnectionResource.WaitForPreviousOwnerAsync(key, 1500);
        try { Assert.False(reopening.IsCompleted); }
        finally { raw.ReleaseClose.Set(); }
        await reopening;
        Assert.True(old.IsClosed);
        var next = new CanConnectionResource(new Driver(), key); await next.CloseAsync();
    }

    [Fact]
    public async Task Failed_old_close_can_be_retried_after_execution_registry_disappears()
    {
        string key = Guid.NewGuid().ToString(); var raw = new Driver { FailClose = true };
        var old = new CanConnectionResource(raw, key);
        await Assert.ThrowsAsync<InvalidOperationException>(() => old.CloseAsync());
        Assert.False(old.IsClosed);
        raw.FailClose = false;
        await CanConnectionResource.WaitForPreviousOwnerAsync(key, 1000);
        Assert.True(old.IsClosed); Assert.Equal(2, raw.Closes);
        var next = new CanConnectionResource(new Driver(), key); await next.CloseAsync();
    }

    [Fact]
    public async Task Active_other_connection_is_never_taken_over()
    {
        string key = Guid.NewGuid().ToString(); var raw = new Driver(); var active = new CanConnectionResource(raw, key);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CanConnectionResource.WaitForPreviousOwnerAsync(key, 1000));
            Assert.Contains(key, error.Message); Assert.Equal(0, raw.Closes);
        }
        finally { await active.CloseAsync(); }
    }

    [Fact]
    public async Task Close_cancels_raw_infinite_receive_but_waits_for_real_exit()
    {
        var raw = new Driver(); var owner = new CanConnectionResource(raw);
        var read = Task.Run(() => owner.Read(Timeout.Infinite));
        await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            await owner.CloseAsync(300);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await read);
            Assert.True(owner.IsClosed);
        }
        finally { raw.ReleaseRead.Set(); try { await read; } catch (OperationCanceledException) { } await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Noncooperative_read_preserves_claim_and_phase_diagnostics()
    {
        string key = Guid.NewGuid().ToString(); var raw = new Driver { IgnoreCancellation = true };
        var owner = new CanConnectionResource(raw, key); var read = Task.Run(() => owner.Read(Timeout.Infinite));
        await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); owner.Dispose();
        try
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(() => CanConnectionResource.WaitForPreviousOwnerAsync(key, 40));
            Assert.Contains("实际 I/O", error.ToString()); Assert.Equal(0, raw.Closes);
            Assert.Throws<InvalidOperationException>(() => new CanConnectionResource(new Driver(), key));
        }
        finally { raw.ReleaseRead.Set(); await read; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Disposing_failed_open_owner_again_cannot_remove_new_owner_claim()
    {
        string key = Guid.NewGuid().ToString(); var old = new CanConnectionResource(new Driver { FailOpen = true }, key);
        Assert.Throws<InvalidOperationException>(() => old.Open(new())); Assert.True(old.IsClosed);
        var current = new CanConnectionResource(new Driver(), key);
        try
        {
            old.Dispose(); await old.CloseAsync();
            Assert.Throws<InvalidOperationException>(() => new CanConnectionResource(new Driver(), key));
        }
        finally { await current.CloseAsync(); }
    }

    [Fact]
    public async Task Close_during_open_retains_initializing_native_handle_until_open_exits()
    {
        string key = Guid.NewGuid().ToString(); var raw = new Driver { BlockOpen = true };
        var owner = new CanConnectionResource(raw, key); var opening = Task.Run(() => owner.Open(new()));
        await raw.OpenEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); owner.Dispose();
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => CanConnectionResource.WaitForPreviousOwnerAsync(key, 40));
            Assert.Equal(0, raw.Closes);
        }
        finally { raw.ReleaseOpen.Set(); try { await opening; } catch (OperationCanceledException) { } await owner.CloseAsync(); }
        Assert.True(owner.IsClosed); Assert.False(owner.IsConnected);
    }

    private sealed class Driver : ICanAdapter, ICanCloseState
    {
        public bool BlockClose, BlockOpen, FailClose, FailOpen, IgnoreCancellation;
        public int Closes;
        public ManualResetEventSlim ReleaseClose = new(), ReleaseRead = new(), ReleaseOpen = new();
        public TaskCompletionSource CloseEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource OpenEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsConnected { get; private set; } = true;
        public bool HandlesReleased { get; private set; }
        public void Open(CanAdapterConfig config)
        {
            OpenEntered.TrySetResult(); if (BlockOpen) ReleaseOpen.Wait();
            if (FailOpen) throw new InvalidOperationException("native open failed");
            IsConnected = true;
        }
        public void Close()
        {
            Interlocked.Increment(ref Closes); CloseEntered.TrySetResult(); if (BlockClose) ReleaseClose.Wait();
            if (FailClose) throw new InvalidOperationException("native clear failed");
            HandlesReleased = true; IsConnected = false;
        }
        public void Dispose() { HandlesReleased = true; IsConnected = false; }
        public void Write(CanMessage frame) { }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default)
        {
            ReadEntered.TrySetResult(); ReleaseRead.Wait(IgnoreCancellation ? default : ct); return null;
        }
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
    }
}
