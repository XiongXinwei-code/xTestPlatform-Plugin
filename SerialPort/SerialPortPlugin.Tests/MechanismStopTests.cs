using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using Xunit;

namespace SerialPort.Tests;

public sealed class MechanismStopTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sdk_cleanup_retires_registered_open_waiter_before_the_native_open_exits(bool cleanupAll)
    {
        using var guard = new CancellationTokenSource(15000);
        using var releaseOpen = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new SerialPortOpenPlugin(), new SerialPortOpenSetting());
        state.ResourcesOverride = registry;
        var channel = new Channel();
        var owner = new SerialConnectionResource(channel);
        string name = "mechanism-opening-" + Guid.NewGuid().ToString("N");
        var opening = SerialPortLifecycle.OpenRegisteredAsync(context, name, owner,
            () => { entered.TrySetResult(); releaseOpen.Wait(guard.Token); }, CancellationToken.None, 60000);
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            SdkRegistryForwarder.Cleanup(registry, cleanupAll);

            // Exercise the lifecycle helper itself: an outer executor observer must not
            // hide a catch block that waits again for the unfinished native close.
            await Assert.ThrowsAsync<InvalidOperationException>(() => opening.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(guard.IsCancellationRequested);
            Assert.False(owner.IsClosed);
            Assert.Equal(0, channel.Releases);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30, CancellationToken.None));

            releaseOpen.Set();
            await owner.CloseAsync(2000, guard.Token);
            Assert.True(owner.IsClosed);
            Assert.Equal(1, channel.Releases);
        }
        finally
        {
            releaseOpen.Set();
            try { await opening.WaitAsync(guard.Token); } catch (Exception) { }
            await owner.CloseAsync(2000, guard.Token);
            SdkRegistryForwarder.Cleanup(registry, true);
        }
    }

    [Fact]
    public async Task Owner_stop_retires_active_and_queued_waiters_but_keeps_the_channel_until_actual_read_exit()
    {
        using var guard = new CancellationTokenSource(15000);
        using var releaseRead = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new Channel
        {
            ReadAction = () => { entered.TrySetResult(); releaseRead.Wait(guard.Token); return 1; }
        };
        var owner = new SerialConnectionResource(channel);
        var reading = owner.ReadFrameAsync(1, "", 60000, CancellationToken.None);
        await entered.Task.WaitAsync(guard.Token);
        var queuedWrite = owner.WriteAsync([0x55], CancellationToken.None);
        try
        {
            owner.Dispose();
            await Assert.ThrowsAsync<InvalidOperationException>(() => reading.WaitAsync(TimeSpan.FromSeconds(1)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => queuedWrite.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(owner.IsClosed);
            Assert.Equal(0, channel.Writes);
            Assert.Equal(0, channel.Releases);

            releaseRead.Set();
            await owner.CloseAsync(2000, guard.Token);
            Assert.True(owner.IsClosed);
            Assert.Equal(1, channel.Releases);
        }
        finally
        {
            releaseRead.Set();
            await owner.CloseAsync(2000, guard.Token);
        }
    }

    [Fact]
    public async Task Late_open_cleanup_cannot_remove_a_new_owner_registered_before_its_lookup_returns()
    {
        using var guard = new CancellationTokenSource(15000);
        using var releaseOpen = new ManualResetEventSlim();
        using var releaseLookup = new ManualResetEventSlim();
        var openEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actual = SdkRegistryForwarder.NewActual();
        var registry = SdkRegistryForwarder.Wrap(actual, out var routing);
        var (context, state) = PublicationContextProxy.Create(new SerialPortOpenPlugin(), new SerialPortOpenSetting());
        state.ResourcesOverride = registry;
        string name = "late-cleanup-" + Guid.NewGuid().ToString("N"), key = SerialPortHelper.GetPortKey(name);
        var old = new SerialConnectionResource(new Channel());
        var fresh = new SerialConnectionResource(new Channel());
        int lookupHeld = 0;
        routing.OnTryGet = (path, type) =>
        {
            if (path == key && type == typeof(SerialConnectionResource) && old.IsClosed && Interlocked.Exchange(ref lookupHeld, 1) == 0)
            {
                lookupEntered.TrySetResult();
                releaseLookup.Wait(guard.Token);
            }
        };
        var opening = SerialPortLifecycle.OpenRegisteredAsync(context, name, old,
            () => { openEntered.TrySetResult(); releaseOpen.Wait(guard.Token); }, default, 60000);
        try
        {
            await openEntered.Task.WaitAsync(guard.Token);
            var closing = old.CloseAsync(5000, guard.Token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => opening.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(old.IsClosed);
            releaseOpen.Set();
            await closing;
            await lookupEntered.Task.WaitAsync(guard.Token);

            actual.Set(key, fresh, ResourceLifetime.Execution);
            releaseLookup.Set();
            // The old remover holds this lifecycle lock over its captured lookup.
            // Acquiring it proves the delayed remover has finished its identity check.
            await Task.Run(() => { lock (SerialPortHelper.RegistryLock(registry)) { } }, guard.Token);
            Assert.True(actual.TryGet<SerialConnectionResource>(key, out var current));
            Assert.Same(fresh, current);
            Assert.False(fresh.IsClosed);
        }
        finally
        {
            releaseOpen.Set(); releaseLookup.Set(); routing.OnTryGet = null;
            try { await opening.WaitAsync(guard.Token); } catch (InvalidOperationException) { }
            await old.CloseAsync(2000, guard.Token);
            await fresh.CloseAsync(2000, guard.Token);
            SdkRegistryForwarder.Cleanup(actual, true);
        }
    }

    private sealed class Channel : ISerialChannel
    {
        internal Func<int>? ReadAction;
        internal int Writes;
        internal int Releases;
        public bool IsOpen => Volatile.Read(ref Releases) == 0;
        public int ReadTimeout { get; set; } = 60000;
        public int WriteTimeout { get; set; } = 60000;
        public int Read(byte[] buffer, int offset, int count) { buffer[offset] = 0x5A; return ReadAction?.Invoke() ?? 1; }
        public void Write(byte[] bytes) => Interlocked.Increment(ref Writes);
        public void Dispose() => Interlocked.Increment(ref Releases);
    }
}
