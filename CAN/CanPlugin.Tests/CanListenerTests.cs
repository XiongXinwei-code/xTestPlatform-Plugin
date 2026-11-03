using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanListenerTests
{
    private static CanLatestRoute Route(uint id = 0x181) => new(id, CanFrameType.Standard, CanReceiveFrameFormat.Any, 1000);

    [Fact]
    public async Task Latest_observer_is_bounded_and_does_not_consume_protocol_or_raw_frames()
    {
        var router = new CanReceiveRouter();
        using var latest = router.Listen([Route(), Route(0x281)]);
        for (int i = 0; i < 500; i++) router.Publish(new() { Id = 0x181, Data = [(byte)i] });
        var snapshot = latest.Snapshot();
        Assert.Equal(500, snapshot.ReceivedFrames); Assert.Equal(499, snapshot.Replacements);
        Assert.Equal(2, snapshot.Frames.Length); Assert.Equal(unchecked((byte)499), snapshot.Frames[0].Frame!.Data[0]);
        Assert.Equal("TimeUnknown", snapshot.Frames[0].Quality); Assert.False(snapshot.Frames[0].Valid);
        Assert.Equal("NeverReceived", snapshot.Frames[1].Quality);
        Assert.NotNull(await router.ReadRawAsync(0x181, 100, default, null));
        Assert.NotNull(await router.ReadAsync(0x181, 100, default));
    }

    [Fact]
    public void Old_capture_time_and_first_receive_failure_survive_snapshot_and_dispose()
    {
        var router = new CanReceiveRouter(); var latest = router.Listen([Route()]);
        router.Publish(new() { Id = 0x181, Data = [1], CaptureMonotonicLowerBoundTicks = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 2 });
        Assert.Equal("Stale", latest.Snapshot().Frames[0].Quality);
        router.Finish(new InvalidOperationException("fake receiver failed")); latest.Dispose();
        Assert.Contains("fake receiver failed", latest.Snapshot().Error);
    }

    [Fact]
    public async Task Raw_listener_updates_latest_then_successful_stop_blocks_late_publication_and_allows_restart()
    {
        var (context, state, raw, owner) = Setup();
        try
        {
            using var caller = new CancellationTokenSource();
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context, caller.Token)).StepResult.Status);
            caller.Cancel(); // Startup step's temporary token does not own a successful listener.
            raw.Incoming.Add(new() { Id = 0x181, Data = [1] });
            await Until(() => Equals(state.Variables["Locals.LatestRaw"], "01"));
            raw.Incoming.Add(new() { Id = 0x181, IsFd = true, Data = [2, 3] });
            await Until(() => Equals(state.Variables["Locals.LatestRaw"], "02 03"));
            await owner.StopBackgroundReadAsync("CanRx1", 1000, default);
            raw.Incoming.Add(new() { Id = 0x181, Data = [9] });
            await Task.Delay(40); Assert.Equal("02 03", state.Variables["Locals.LatestRaw"]);
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            await Task.Delay(30); Assert.Equal("02 03", state.Variables["Locals.LatestRaw"]); // No backlog replay.
            raw.Incoming.Add(new() { Id = 0x181, Data = [4] });
            await Until(() => Equals(state.Variables["Locals.LatestRaw"], "04"));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Invalidated_adapter_clock_is_not_reported_fresh_by_listener_or_status()
    {
        var (context, state, _, unused) = Setup(); await unused.CloseAsync();
        var raw = new ListenerClockAdapter(); var owner = new CanConnectionResource(raw); state.Adapter = owner;
        var guard = new CanExecutionGuard(context, default);
        var lease = CanBackgroundOutputLease.Acquire(context, guard, ["Locals.LatestRaw"]);
        CanLatestSnapshot? latest = null;
        try
        {
            await owner.StartBackgroundReadAsync("clock", [Route()], 5, 1000, default, lease, (s, ct) =>
            { Volatile.Write(ref latest, s); return Task.CompletedTask; }, "Raw");
            raw.Incoming.Add(new() { Id = 0x181, Data = [1], ClockId = "clock1", TimestampDomain = "Device",
                CaptureMonotonicLowerBoundTicks = Stopwatch.GetTimestamp() });
            await Until(() => Volatile.Read(ref latest)?.Frames[0].Valid == true);
            raw.Current = false;
            await Until(() => Volatile.Read(ref latest)?.Frames[0].Quality == "TimeUnknown");
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(owner.GetBackgroundReadStatuses()));
            Assert.False(json.RootElement[0].GetProperty("Frames")[0].GetProperty("Valid").GetBoolean());
            Assert.Null(Volatile.Read(ref latest)!.Frames[0].AgeUpperBoundMs);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Background_receive_failure_is_visible_after_worker_has_exited()
    {
        var (context, state, raw, owner) = Setup();
        try
        {
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            raw.ReadFailure = new InvalidOperationException("listener native receive broke");
            await Until(() => owner.IsolationReason != null);
            await Until(() => !owner.HasPendingManagedWorkers);
            string status = JsonSerializer.Serialize(owner.GetBackgroundReadStatuses());
            Assert.Contains("Faulted", status); Assert.Contains("listener native receive broke", status);
            Assert.Contains("listener native receive broke", await owner.StopBackgroundReadAsync("CanRx1", 1000, default));
            Assert.Contains("listener native receive broke", JsonSerializer.Serialize(owner.GetBackgroundReadStatuses()));
        }
        finally { try { await owner.CloseAsync(); } catch (InvalidOperationException) when (raw.CloseCount > 0) { } }
    }

    private static (IExecutionContext, CaptureContext, FakeAdapter, CanConnectionResource) Setup()
    {
        var context = DispatchProxy.Create<IExecutionContext, CaptureContext>(); var state = (CaptureContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        state.Adapter = owner; state.Plugin = new CanReadBackgroundStartPlugin(); state.Variables["Locals.LatestRaw"] = "";
        state.Setting = new CanReadBackgroundSetting { PublishIntervalMs = 5, Mappings = [new() { CanId = "\"0x181\"", DataVariable = "Locals.LatestRaw" }] };
        return (context, state, raw, owner);
    }
    private static async Task Until(Func<bool> condition)
    {
        using var stop = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(5, stop.Token);
    }
    private sealed class ListenerClockAdapter : FakeAdapter, ICanClockValidity
    {
        internal bool Current = true;
        public bool IsClockCurrent(string clockId) => Current;
    }
}
