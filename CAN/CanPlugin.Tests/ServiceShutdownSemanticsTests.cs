using System.Reflection;
using System.Text.Json;
using CAN.CANopen.Cyclic;
using CAN.Helpers;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class ServiceShutdownSemanticsTests
{
    [Fact]
    public async Task Parent_close_preserves_service_callback_failure_in_the_service_snapshot()
    {
        var owner = new CanConnectionResource(new ConfirmedAdapter());
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 100)]);
        service.StartMonitoring(new(0x80, [0]), new(10, 1000, 1000));
        // Inject a legitimate failing cancellation callback into this service's
        // private lifetime; no SDK or device is involved in the fixture.
        var stop = (CancellationTokenSource)typeof(CanopenCyclicService)
            .GetField("_stop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        using var callback = stop.Token.Register(() => throw new InvalidOperationException("fake service cancel failure"));
        await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync());
        Assert.True(owner.IsClosed); Assert.True(service.IsClosed);
        Assert.Contains("fake service cancel failure", service.Snapshot().ShutdownHistory);
        Assert.Contains("fake service cancel failure", service.Snapshot().BackgroundError);
    }

    [Fact]
    public async Task Pure_monitoring_unknown_transmit_is_not_relabelled_as_confirmed_by_parent_close()
    {
        var raw = new ConfirmedAdapter { OnWrite = _ => throw new InvalidOperationException("fake missing monitoring confirmation") };
        var owner = new CanConnectionResource(raw);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 100)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10, 1000, 1000));
            using var guard = new CancellationTokenSource(5000);
            while (!service.Snapshot().Cycle.HostExited) await Task.Delay(2, guard.Token);
            await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync());
            var snapshot = service.Snapshot();
            Assert.True(owner.IsClosed); Assert.True(snapshot.Cycle.ServiceClosed);
            Assert.False(snapshot.Cycle.PartialCycle);
            Assert.True(snapshot.Cycle.OwnedTxQuiescent); // The SDK connection is now closed.
            Assert.Equal("SdkConnectionClosed", snapshot.Cycle.OwnedTxQuiescenceEvidence);
            Assert.True(snapshot.Cycle.SubmittedFrames > snapshot.Cycle.ConfirmedFrames);
            Assert.False(snapshot.ServiceShutdownConfirmed); // Closing cannot invent frame confirmation.
            Assert.Equal("NotConfirmed", snapshot.ServiceShutdownState);
            Assert.False(snapshot.Cycle.StopCommandsAcknowledged);
        }
        finally { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task An_already_closed_service_does_not_turn_an_invalid_stop_budget_into_success()
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var owner = new CanConnectionResource(new ConfirmedAdapter());
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 100)]);
        await service.CloseAsync();
        try
        {
            var plugin = new ServiceStopPlugin();
            var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
            var state = (SdkCanContext)(object)context;
            state.Registry = registry; state.Plugin = plugin;
            state.Setting = new CyclicSetting { ConnectionName = "\"CAN1\"", ServiceName = "\"test\"", StopTimeoutMs = 0 };
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmed_shutdown_reports_completion_without_erasing_the_original_fault_or_inventing_motor_ack(bool warningSinkFails)
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var raw = new ConfirmedAdapter { OnWrite = _ => Thread.Sleep(12) };
        var owner = new CanConnectionResource(raw);
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 100)]);
        service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(1, 1000, 1000));
        try
        {
            using var guard = new CancellationTokenSource(5000);
            while (!service.Snapshot().Cycle.HostExited) await Task.Delay(2, guard.Token);
            var before = service.Snapshot();
            Assert.True(before.Cycle.OwnedTxQuiescent); Assert.NotNull(before.Cycle.Error);
            Assert.True(before.Receive.Running); // RX is a separate, connection-owned task.
            var plugin = new ServiceStopPlugin();
            var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
            var state = (SdkCanContext)(object)context;
            state.Registry = registry; state.Plugin = plugin;
            state.Setting = new CyclicSetting { ConnectionName = "\"CAN1\"", ServiceName = "\"test\"", StopTimeoutMs = 1000 };
            if (warningSinkFails) state.OnLog = _ => throw new InvalidOperationException("fake warning sink failure");
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(warningSinkFails ? TestStatus.Error : TestStatus.Completed, result.StepResult.Status);
            Assert.Throws<InvalidOperationException>(() => owner.GetService("test"));
            if (warningSinkFails)
            {
                Assert.Contains("fake warning sink failure", result.StepResult.Error!.Message);
                Assert.Equal(before.Cycle.Error, service.Snapshot().Cycle.Error);
                return;
            }
            using var json = JsonDocument.Parse(result.StepResult.Value!);
            Assert.True(json.RootElement.GetProperty("ServiceShutdownConfirmed").GetBoolean());
            Assert.False(json.RootElement.GetProperty("ServiceHealthy").GetBoolean());
            Assert.Contains("发送周期执行超限", json.RootElement.GetProperty("BackgroundError").GetString());
            Assert.Equal(before.Cycle.Error, json.RootElement.GetProperty("Cycle").GetProperty("Error").GetString());
            Assert.False(service.Snapshot().Cycle.StopCommandsAcknowledged);
            Assert.False(service.Snapshot().Cycle.MotorStoppedVerified);
            Assert.Contains(state.Logs, line => line.Contains("历史故障") && line.Contains("FailurePhase"));
        }
        finally { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partial_or_unknown_transmit_does_not_become_a_successful_ServiceStop_but_parent_can_close(bool unknown)
    {
        using var release = new ManualResetEventSlim();
        var raw = new ConfirmedAdapter();
        if (unknown) raw.OnWrite = _ => throw new InvalidOperationException("fake unknown transmit");
        else raw.WriteBlock = release;
        var owner = new CanConnectionResource(raw);
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 100)]);
        service.StartTargets([1], [new(0x201, [1]), new(0x301, [2])], new(0x80, [0]), new(1000, 5000, 1000));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var plugin = new ServiceStopPlugin();
            var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
            var state = (SdkCanContext)(object)context;
            state.Registry = registry; state.Plugin = plugin;
            state.Setting = new CyclicSetting { ConnectionName = "\"CAN1\"", ServiceName = "\"test\"", StopTimeoutMs = 1000 };
            var stopping = plugin.CreateExecutor().ExecuteAsync(context);
            if (!unknown)
            {
                using var guard = new CancellationTokenSource(5000);
                while (service.Snapshot().Cycle.State != "Stopping") await Task.Delay(2, guard.Token);
                Assert.False(stopping.IsCompleted); Assert.Equal(0, raw.CloseCount);
                release.Set();
            }
            Assert.Equal(TestStatus.Error, (await stopping).StepResult.Status);
            Assert.False(service.IsClosed); Assert.True(service.Snapshot().Cycle.PartialCycle);
            if (!unknown) Assert.Equal(service.Snapshot().Cycle.SubmittedFrames, service.Snapshot().Cycle.ConfirmedFrames);
            await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync());
            Assert.True(owner.IsClosed); Assert.Equal(1, raw.CloseCount);
            Assert.False(service.Snapshot().Cycle.StopCommandsAcknowledged);
            Assert.False(service.Snapshot().Cycle.MotorStoppedVerified);
            Assert.Equal("ClosedWithUnconfirmedTransmit", service.Snapshot().Cycle.State);
        }
        finally { release.Set(); try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Theory]
    [InlineData(1, 120)]
    [InlineData(4, 120)]
    [InlineData(4, 156)]
    [InlineData(7, 156)]
    public void Router_fifo_and_latest_observer_statistics_are_independent_from_adapter_queue_depth(int routes, int framesPerRoute)
    {
        var router = new CanReceiveRouter();
        using var subscription = router.Subscribe(Enumerable.Range(1, routes)
            .Select(i => new FeedbackRoute("feedback" + i, (uint)(0x80 + i), 1, 1000)).ToArray());
        for (int n = 0; n < framesPerRoute; n++)
            for (int i = 1; i <= routes; i++) router.Publish(new() { Id = (uint)(0x80 + i), Data = [(byte)n] });
        var stats = router.Diagnostics;
        Assert.Equal(routes * 64, stats.Depth);
        Assert.Equal(routes * (framesPerRoute - 64), stats.Evictions);
        Assert.Equal(routes * (framesPerRoute - 1), subscription.Replacements);
        var receive = new ReceiveSnapshot(Guid.NewGuid(), subscription.Epoch, true, subscription.Snapshot(),
            stats.Depth, stats.Evictions, subscription.Replacements, new(null, null, 0, null), null);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(receive));
        Assert.Equal("CANopenRouter.PerIdFifo", json.RootElement.GetProperty("PendingFramesScope").GetString());
        Assert.Equal("Adapter", json.RootElement.GetProperty("QueueStatisticsScope").GetString());
        Assert.Equal(64, json.RootElement.GetProperty("RouterCapacityPerId").GetInt32());
    }
}
