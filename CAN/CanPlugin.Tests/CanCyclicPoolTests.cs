using System.Diagnostics;
using System.Text.Json;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanCyclicPoolTests
{
    private static CanCyclicDefinition Definition(uint id, byte value = 1, int period = 10, bool fd = false,
        CanFrameType type = CanFrameType.Standard, uint initial = 0, uint maximum = 255,
        Func<uint>? dynamicId = null, Func<CanCyclicControl>? control = null) => new(id, type, fd,
        control?.Invoke() ?? new(true, period), 2, 2000,
        new() { RcEnabled = true, RcInitialValue = initial, RcMaximum = maximum },
        (_, check) => { check(); return Task.FromResult(new CanMessage { Id = dynamicId?.Invoke() ?? id, FrameType = type, IsFd = fd, Data = [0, value] }); },
        (_, check) => { check(); return Task.FromResult(control?.Invoke() ?? new CanCyclicControl(true, period)); },
        () => { }, () => Task.CompletedTask);

    private static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(5, deadline.Token);
    }
    private static JsonElement Status(CanConnectionResource owner, string name) =>
        JsonSerializer.SerializeToElement(owner.GetCyclicStatuses()).EnumerateArray().Single(s => s.GetProperty("TaskName").GetString() == name);

    [Fact]
    public async Task Later_task_replaces_only_duplicate_ID_and_old_stop_cannot_remove_new_owner()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            owner.UpdateCyclicPool("old", "CAN", [Definition(0x111, 1), Definition(0x222, 2)], () => { });
            await Until(() => raw.Sent.Any(f => f.Id == 0x111) && raw.Sent.Any(f => f.Id == 0x222));
            owner.UpdateCyclicPool("new", "DBC", [Definition(0x111, 3, fd: true)], () => { });
            await Until(() => raw.Sent.Any(f => f.Id == 0x111 && f.Data[1] == 3));
            await owner.StopJobAsync("old", 1000, default);
            int count = raw.Sent.Count; await Until(() => raw.Sent.Count > count + 2);
            Assert.All(raw.Sent.Skip(count), frame => { Assert.Equal(0x111u, frame.Id); Assert.Equal(3, frame.Data[1]); Assert.True(frame.IsFd); });
            Assert.True(Status(owner, "new").GetProperty("Active").GetBoolean());
            Assert.Contains(Status(owner, "old").GetProperty("Messages").EnumerateArray(), row => row.GetProperty("State").GetString() == "Superseded");
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Same_name_updates_keep_unmentioned_ID_and_counter_progress()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            owner.UpdateCyclicPool("same", "CAN", [Definition(0x111, 1, initial: 40), Definition(0x222, 2)], () => { });
            await Until(() => raw.Sent.Count(f => f.Id == 0x111) >= 3);
            owner.UpdateCyclicPool("same", "CAN", [Definition(0x111, 9, initial: 0)], () => { });
            await Until(() => raw.Sent.Count(f => f.Id == 0x111 && f.Data[1] == 9) >= 3);
            await owner.StopJobAsync("same", 1000, default);
            Assert.Equal(Enumerable.Range(40, raw.Sent.Count(f => f.Id == 0x111)).Select(i => (byte)i), raw.Sent.Where(f => f.Id == 0x111).Select(f => f.Data[0]));
            Assert.True(raw.Sent.Count(f => f.Id == 0x222) >= 3);
            int count = raw.Sent.Count; await Task.Delay(30); Assert.Equal(count, raw.Sent.Count);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Update_returns_without_initial_frame_and_waits_for_entered_native_write()
    {
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release };
        var owner = new CanConnectionResource(raw);
        try
        {
            owner.UpdateCyclicPool("old", "CAN", [Definition(0x111, 1, initial: 10)], () => { });
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            owner.UpdateCyclicPool("new", "CAN", [Definition(0x111, 9)], () => { });
            Assert.Empty(raw.Sent);
            Assert.True(Status(owner, "new").GetProperty("Messages")[0].GetProperty("PendingApply").GetBoolean());
            release.Set();
            await Until(() => raw.Sent.Count >= 3);
            await owner.StopJobAsync("new", 1000, default);
            var frames = raw.Sent.ToArray(); Assert.Equal(1, frames[0].Data[1]);
            Assert.All(frames.Skip(1), frame => Assert.Equal(9, frame.Data[1]));
            Assert.Equal(Enumerable.Range(10, frames.Length).Select(i => (byte)i), frames.Select(f => f.Data[0]));
        }
        finally { release.Set(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Invalid_later_RC_range_leaves_every_old_owner_unchanged()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            owner.UpdateCyclicPool("old", "CAN", [Definition(0x111, 1), Definition(0x222, 2, initial: 100)], () => { });
            await Until(() => raw.Sent.Count >= 2);
            Assert.Throws<ArgumentException>(() => owner.UpdateCyclicPool("bad", "DBC",
                [Definition(0x111, 8), Definition(0x222, 8, maximum: 10)], () => { }));
            int count = raw.Sent.Count; await Until(() => raw.Sent.Count >= count + 3);
            Assert.DoesNotContain(raw.Sent, frame => frame.Data[1] == 8);
            Assert.Single(owner.GetCyclicStatuses());
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Dynamic_old_ID_cannot_reclaim_newer_slot_even_after_new_owner_stops()
    {
        uint current = 0x111; var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            owner.UpdateCyclicPool("old", "CAN", [Definition(0x111, 1, dynamicId: () => Volatile.Read(ref current))], () => { });
            await Until(() => raw.Sent.Count > 0);
            owner.UpdateCyclicPool("new", "DBC", [Definition(0x222, 9)], () => { });
            await Until(() => raw.Sent.Any(f => f.Id == 0x222));
            await owner.StopJobAsync("new", 1000, default);
            Volatile.Write(ref current, 0x222);
            await Until(() => Status(owner, "old").GetProperty("Messages")[0].GetProperty("State").GetString() == "Superseded");
            Assert.DoesNotContain(raw.Sent, frame => frame.Id == 0x222 && frame.Data[1] == 1);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Stop_then_start_resets_counter_but_pause_resume_does_not()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); bool enabled = true;
        try
        {
            owner.UpdateCyclicPool("one", "CAN", [Definition(0x111, initial: 7, control: () => new(Volatile.Read(ref enabled), 10))], () => { });
            await Until(() => raw.Sent.Count >= 3); Volatile.Write(ref enabled, false);
            await Until(() => Status(owner, "one").GetProperty("Messages")[0].GetProperty("State").GetString() == "Paused");
            int count = raw.Sent.Count; await Task.Delay(30); Assert.Equal(count, raw.Sent.Count);
            Volatile.Write(ref enabled, true); await Until(() => raw.Sent.Count >= count + 2);
            await owner.StopJobAsync("one", 1000, default);
            Assert.Equal(Enumerable.Range(7, raw.Sent.Count).Select(i => (byte)i), raw.Sent.Select(f => f.Data[0]));
            count = raw.Sent.Count; owner.UpdateCyclicPool("one", "CAN", [Definition(0x111, initial: 20)], () => { });
            await Until(() => raw.Sent.Count > count); await owner.StopJobAsync("one", 1000, default);
            Assert.Equal(20, raw.Sent.ElementAt(count).Data[0]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Dynamic_ID_roundtrip_preserves_each_ID_counter_and_uses_initial_counter_for_new_ID()
    {
        uint id = 0x111; var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            owner.UpdateCyclicPool("dynamic", "CAN", [Definition(id, initial: 30, dynamicId: () => Volatile.Read(ref id))], () => { });
            await Until(() => raw.Sent.Count(f => f.Id == 0x111) >= 2);
            Volatile.Write(ref id, 0x222); await Until(() => raw.Sent.Count(f => f.Id == 0x222) >= 2);
            Volatile.Write(ref id, 0x111); await Until(() => raw.Sent.Last().Id == 0x111);
            await owner.StopJobAsync("dynamic", 1000, default);
            foreach (uint canId in new uint[] { 0x111, 0x222 })
            {
                var frames = raw.Sent.Where(f => f.Id == canId).ToArray();
                Assert.Equal(Enumerable.Range(30, frames.Length).Select(i => (byte)i), frames.Select(f => f.Data[0]));
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Slow_expression_cannot_block_another_ID_and_stop_waits_for_actual_expression_exit()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var slow = Definition(0x111) with { Build = async (_, check) => { entered.TrySetResult(); await release.Task; check(); return new() { Id = 0x111, Data = [0, 1] }; } };
            owner.UpdateCyclicPool("slow", "CAN", [slow], () => { });
            owner.UpdateCyclicPool("fast", "CAN", [Definition(0x222)], () => { });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Until(() => raw.Sent.Count(f => f.Id == 0x222) >= 3);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.StopJobAsync("slow", 20, default));
            release.SetResult(); await owner.StopJobAsync("slow", 1000, default);
            Assert.DoesNotContain(raw.Sent, frame => frame.Id == 0x111);
        }
        finally { release.TrySetResult(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Dynamic_period_change_reschedules_without_waiting_for_old_long_period()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); int period = 60000;
        try
        {
            owner.UpdateCyclicPool("dynamic", "CAN", [Definition(0x111, control: () => new(true, Volatile.Read(ref period)))], () => { });
            Assert.Empty(raw.Sent); Volatile.Write(ref period, 10);
            await Until(() => raw.Sent.Count >= 2);
            Volatile.Write(ref period, 60000);
            await Until(() => Status(owner, "dynamic").GetProperty("Messages")[0].GetProperty("CycleTimeMs").GetInt32() == 60000);
            int count = raw.Sent.Count; await Task.Delay(50); Assert.Equal(count, raw.Sent.Count);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Repeated_updates_release_old_contexts_and_bound_status_history()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            for (int i = 0; i < 300; i++) owner.UpdateCyclicPool("same", "CAN", [Definition(0x111, period: 60000)], () => { });
            await Until(() => Status(owner, "same").GetProperty("Messages").GetArrayLength() < 5);
            Assert.Empty(raw.Sent);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Paused_then_resumed_pending_build_cannot_publish_the_old_payload()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); bool enabled = true;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        try
        {
            var definition = Definition(0x111, control: () => new(Volatile.Read(ref enabled), 10)) with
            {
                Build = async (_, _) =>
                {
                    if (Interlocked.Increment(ref calls) == 1) { entered.TrySetResult(); await release.Task; return new() { Id = 0x111, Data = [0, 1] }; }
                    return new() { Id = 0x111, Data = [0, 9] };
                }
            };
            owner.UpdateCyclicPool("dynamic", "CAN", [definition], () => { });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Volatile.Write(ref enabled, false);
            await Until(() => Status(owner, "dynamic").GetProperty("Messages")[0].GetProperty("State").GetString() == "Paused");
            release.SetResult(); Volatile.Write(ref enabled, true);
            await Until(() => raw.Sent.Count >= 2); await owner.StopJobAsync("dynamic", 1000, default);
            Assert.All(raw.Sent, frame => Assert.Equal(9, frame.Data[1]));
            Assert.Equal(0, raw.Sent.First().Data[0]);
        }
        finally { release.TrySetResult(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Batch_exit_waits_for_cancellation_callback_that_began_after_Finish()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var actual = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var batch = new CanConnectionResource.CanCyclicBatch(owner, [], CancellationToken.None);
        using var callback = batch.Token.Register(() => { entered.TrySetResult(); release.Wait(); });
        try
        {
            batch.TrackActual(actual.Task); batch.Finish(); batch.Cancel();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); actual.SetResult();
            await Task.Delay(25); Assert.False(batch.Completion.IsCompleted);
            release.Set(); await batch.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { release.Set(); actual.TrySetResult(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Idle_pool_host_does_not_block_diagnostic_sender_after_every_config_is_stopped()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        try
        {
            owner.UpdateCyclicPool("one", "CAN", [Definition(0x111, control: () => new(false, 10))], () => { });
            var diagnostic = owner.StartService("diagnostic", []);
            Assert.Throws<InvalidOperationException>(() => owner.EnterSenderStart(diagnostic, true));
            await owner.StopJobAsync("one", 1000, default);
            using var start = owner.EnterSenderStart(diagnostic, true);
            owner.ReserveSender(diagnostic, [0x080], [0x080], true);
            owner.ReleaseSender(diagnostic);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Normal_stop_waits_for_entered_native_frame_without_isolating_connection_and_allows_restart()
    {
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release };
        var owner = new CanConnectionResource(raw);
        try
        {
            owner.UpdateCyclicPool("same", "CAN", [Definition(0x111, initial: 7)], () => { });
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stopped = owner.StopJobAsync("same", 2000, default);
            await Task.Delay(20); Assert.False(stopped.IsCompleted);
            Assert.Equal("Stopping", Status(owner, "same").GetProperty("Messages")[0].GetProperty("State").GetString());
            Assert.True(Status(owner, "same").GetProperty("Messages")[0].GetProperty("ExitPending").GetBoolean());
            release.Set(); await stopped;
            Assert.Equal("Stopped", Status(owner, "same").GetProperty("Messages")[0].GetProperty("State").GetString());
            Assert.False(Status(owner, "same").GetProperty("Messages")[0].GetProperty("ExitPending").GetBoolean());
            Assert.Single(raw.Sent);
            owner.UpdateCyclicPool("same", "CAN", [Definition(0x111, initial: 20)], () => { });
            await Until(() => raw.Sent.Count >= 2); await owner.StopJobAsync("same", 1000, default);
            Assert.Equal(20, raw.Sent.ElementAt(1).Data[0]);
        }
        finally { release.Set(); await owner.CloseAsync(); }
    }
}
