using System.Reflection;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class AutoDisposeFollowupTests
{
    [Fact]
    public async Task Sdk_disposal_keeps_the_configured_stop_budget_beyond_the_legacy_five_second_wait()
    {
        using var guard = new CancellationTokenSource(15000);
        var registry = (IResourceRegistry)Activator.CreateInstance(
            typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var raw = new ConfirmedAdapter();
        var owner = new CanConnectionResource(raw);
        registry.Set("owned-can", owner, ResourceLifetime.Execution);
        var firstCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task response = Task.CompletedTask;
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x80) firstCycle.TrySetResult();
            if (frame.Id != 0x601) return;
            stopSent.TrySetResult();
            response = Task.Run(async () =>
            {
                await Task.Delay(5500);
                raw.Incoming.Add(new() { Id = 0x581, Data = [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
            });
        };
        var service = owner.StartService("owned", [new("A", 0x181, 1, 100)]);
        try
        {
            service.StartConfiguredTargets([1], [new(0x201, [1])], new(0x80, []),
                new(1000, 8000, 1000, true, 20000), new([new(1, 0x2000, 0, [1])], 8000));
            await firstCycle.Task.WaitAsync(guard.Token);
            var sdkCleanup = Task.Run(() => registry.GetType().GetMethod("CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null));
            await sdkCleanup.WaitAsync(TimeSpan.FromSeconds(2), guard.Token);
            await stopSent.Task.WaitAsync(guard.Token);
            Assert.False(owner.IsClosed); // IDisposable returned while its actual stop is still pending.
            await owner.RequestClose().WaitAsync(guard.Token);
            Assert.True(owner.IsClosed);
            Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
            Assert.Equal(1, raw.CloseCount);
        }
        finally
        {
            await response;
            try { await owner.CloseAsync(8000, guard.Token); }
            catch (Exception) when (owner.IsClosed) { }
            registry.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
        }
    }

    [Fact]
    public async Task Explicit_close_keeps_its_soft_budget_and_never_releases_a_driver_with_pending_read()
    {
        using var release = new ManualResetEventSlim();
        var raw = new FakeAdapter { ReadBlock = release };
        var owner = new CanConnectionResource(raw);
        owner.StartService("receive", [new("A", 0x181, 1, 100)]);
        try
        {
            await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount);
            Assert.NotNull(owner.IsolationReason);
        }
        finally { release.Set(); await owner.RequestClose().WaitAsync(TimeSpan.FromSeconds(3)); }
        Assert.True(owner.IsClosed); Assert.Equal(1, raw.CloseCount);
    }
}
