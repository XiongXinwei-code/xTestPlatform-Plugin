using System.Buffers;
using System.Reflection;
using System.Text;
using CAN.Models;
using CAN.Runtime;
using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanPayloadSendTests
{
    private static CanPayloadProtectionSetting Counter(uint start = 0) => new() { RcEnabled = true, RcInitialValue = start };
    private static CanMessage Frame(uint id = 0x123) => new() { Id = id, Data = [0xAA, 0x55] };

    [Fact]
    public async Task Parallel_calls_increment_in_native_submission_order_and_keep_input_unchanged()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); var frame = Frame();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => owner.WriteProtectedAsync(frame, Counter(), "stepA", 10000)));
            Assert.Equal(Enumerable.Range(0, 32).Select(i => (byte)i), raw.Sent.Select(f => f.Data[0]));
            Assert.All(raw.Sent, f => Assert.Equal(0x55, f.Data[1])); Assert.Equal(0xAA, frame.Data[0]);
            await owner.WriteProtectedAsync(frame, Counter(10), "stepB", 1000);
            await owner.WriteProtectedAsync(Frame(0x124), Counter(), "stepA", 1000);
            Assert.Equal(new byte[] { 10, 0 }, raw.Sent.Skip(32).Select(f => f.Data[0]));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Queued_cancellation_neither_sends_nor_consumes_counter()
    {
        using var release = new ManualResetEventSlim(); using var cancel = new CancellationTokenSource();
        var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        var blocking = Task.Run(() => owner.Write(Frame(0x124)));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var queued = owner.WriteProtectedAsync(Frame(), Counter(7), "stepA", 5000, cancel.Token);
            await Task.Delay(50); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            release.Set(); await blocking;
            await owner.WriteProtectedAsync(Frame(), Counter(7), "stepA", 1000);
            Assert.Equal(7, Assert.Single(raw.Sent, f => f.Id == 0x123).Data[0]);
        }
        finally { release.Set(); await blocking; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Unknown_driver_failure_consumes_attempt_and_does_not_repeat_the_value()
    {
        var raw = new FakeAdapter { OnWrite = _ => throw new InvalidOperationException("unknown acceptance") };
        var owner = new CanConnectionResource(raw);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.WriteProtectedAsync(Frame(), Counter(5), "stepA", 1000));
            raw.OnWrite = null;
            await owner.WriteProtectedAsync(Frame(), Counter(5), "stepA", 1000);
            Assert.Equal(6, Assert.Single(raw.Sent).Data[0]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Cancelled_inflight_protected_write_retains_native_owner_and_new_epoch_starts_fresh()
    {
        using var release = new ManualResetEventSlim(); using var cancel = new CancellationTokenSource();
        var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        var sending = owner.WriteProtectedAsync(Frame(), Counter(7), "stepA", 5000, cancel.Token);
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
            Assert.NotNull(owner.IsolationReason);
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.WriteProtectedAsync(Frame(), Counter(7), "stepA", 1000));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount);
        }
        finally { release.Set(); await owner.CloseAsync(); }
        Assert.Equal(7, Assert.Single(raw.Sent).Data[0]);
        var freshRaw = new FakeAdapter(); var fresh = new CanConnectionResource(freshRaw);
        try { await fresh.WriteProtectedAsync(Frame(), Counter(7), "stepA", 1000); Assert.Equal(7, Assert.Single(freshRaw.Sent).Data[0]); }
        finally { await fresh.CloseAsync(); }
    }

    [Fact]
    public async Task Disable_reenable_and_new_connection_reset_the_counter()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            await owner.WriteProtectedAsync(Frame(), Counter(2), "stepA", 1000);
            await owner.WriteProtectedAsync(Frame(), Counter(2), "stepA", 1000);
            await owner.WriteProtectedAsync(Frame(), new(), "stepA", 1000);
            await owner.WriteProtectedAsync(Frame(), Counter(2), "stepA", 1000);
            Assert.Equal(new byte[] { 2, 3, 0xAA, 2 }, raw.Sent.Select(f => f.Data[0]));
        }
        finally { await owner.CloseAsync(); }
        var freshRaw = new FakeAdapter(); var fresh = new CanConnectionResource(freshRaw);
        try { await fresh.WriteProtectedAsync(Frame(), Counter(2), "stepA", 1000); Assert.Equal(2, Assert.Single(freshRaw.Sent).Data[0]); }
        finally { await fresh.CloseAsync(); }
    }

    [Fact]
    public async Task Settings_are_frozen_before_a_queued_request_is_submitted()
    {
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release };
        var owner = new CanConnectionResource(raw); var blocking = Task.Run(() => owner.Write(Frame(0x124)));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var setting = Counter(5); var waiting = owner.WriteProtectedAsync(Frame(), setting, "stepA", 5000);
            setting.RcInitialValue = 99; setting.RcStep = 100;
            release.Set(); await blocking; await waiting;
            Assert.Equal(5, Assert.Single(raw.Sent, f => f.Id == 0x123).Data[0]);
        }
        finally { release.Set(); await blocking; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Fd_write_and_executor_result_contain_computed_crc_not_the_template()
    {
        var context = DispatchProxy.Create<IExecutionContext, StablePayloadContext>(); var state = (StablePayloadContext)(object)context;
        state.Plugin = new CanWritePlugin();
        state.Setting = new CanWriteSetting
        {
            Data = "\"31 32 33 34 35 36 37 38 39 00 00\"", UseFdFrame = true,
            Protection = new() { CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc16Modbus, CrcStartByte = 9, CrcEndByte = 10, CrcDataEndByte = 8 }
        };
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); state.Adapter = owner;
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Passed, result.StepResult.Status); Assert.Contains("37 4B", result.StepResult.Value?.ToString());
            Assert.Equal(Encoding.ASCII.GetBytes("123456789").Concat(new byte[] { 0x37, 0x4B }), Assert.Single(raw.Sent).Data);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task New_executor_instances_for_the_same_step_continue_the_counter()
    {
        var context = DispatchProxy.Create<IExecutionContext, StablePayloadContext>(); var state = (StablePayloadContext)(object)context;
        state.Plugin = new CanWritePlugin(); state.Setting = new CanWriteSetting { Data = "\"AA 55\"", Protection = Counter(8) };
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); state.Adapter = owner;
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
                Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.Message);
            }
            Assert.Equal(new byte[] { 8, 9, 10 }, raw.Sent.Select(f => f.Data[0]));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Cyclic_rows_are_independent_and_stop_then_restart_resets_both()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = new CanCyclicSendStartPlugin(); state.Setting = new CanCyclicSendStartSetting { Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 00\"", CycleTimeMs = 5, Protection = Counter(10) },
            new() { CanId = "\"0x124\"", Data = "\"00 00\"", CycleTimeMs = 5, UseFdFrame = true, Protection = Counter(20) }
        ] };
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD }); state.Adapter = owner;
        try
        {
            for (var restart = 0; restart < 2; restart++)
            {
                var offset = raw.Sent.Count;
                Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
                using var timeout = new CancellationTokenSource(5000);
                while (raw.Sent.Skip(offset).Count(f => f.Id == 0x123) < 3 || raw.Sent.Skip(offset).Count(f => f.Id == 0x124) < 3)
                    await Task.Delay(5, timeout.Token);
                await owner.StopJobAsync("CyclicTask1", 1000, default);
                Assert.Equal(new byte[] { 10, 11, 12 }, raw.Sent.Skip(offset).Where(f => f.Id == 0x123).Take(3).Select(f => f.Data[0]));
                Assert.Equal(new byte[] { 20, 21, 22 }, raw.Sent.Skip(offset).Where(f => f.Id == 0x124).Take(3).Select(f => f.Data[0]));
                var stoppedCount = raw.Sent.Count; await Task.Delay(25); Assert.Equal(stoppedCount, raw.Sent.Count);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Stop_removes_queued_protected_cycle_and_restart_uses_initial_counter()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = new CanCyclicSendStartPlugin(); state.Setting = new CanCyclicSendStartSetting { Messages =
            [new() { CanId = "\"0x123\"", Data = "\"00 00\"", CycleTimeMs = 5, Protection = Counter(7) }] };
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release };
        var owner = new CanConnectionResource(raw); state.Adapter = owner;
        var queue = (CanSubmissionGate)typeof(CanConnectionResource).GetField("_txGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        var blocking = Task.Run(() => owner.Write(Frame(0x124)));
        try
        {
            using var timeout = new CancellationTokenSource(5000);
            await raw.WriteEntered.Task.WaitAsync(timeout.Token);
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            while (queue.QueuedCount == 0) await Task.Delay(1, timeout.Token);
            await owner.StopJobAsync("CyclicTask1", 1000, default);
            Assert.Empty(raw.Sent); Assert.Equal(0, queue.QueuedCount);
            release.Set(); await blocking;
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            while (!raw.Sent.Any(f => f.Id == 0x123)) await Task.Delay(1, timeout.Token);
            await owner.StopJobAsync("CyclicTask1", 1000, default);
            Assert.Equal(7, raw.Sent.First(f => f.Id == 0x123).Data[0]);
        }
        finally { release.Set(); await blocking; await owner.CloseAsync(); }
    }

    [Fact]
    public void Legacy_single_send_without_protection_leaves_features_disabled()
    {
        var buffer = new ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer);
        writer.WriteMapHeader(2); writer.Write("Data"); writer.Write("\"AA BB\""); writer.Write("CanId"); writer.Write("\"0x123\""); writer.Flush();
        var settings = (CanWriteSetting)new CanWritePlugin().CreateSerializer().Deserialize(buffer.WrittenSpan.ToArray(), 1);
        Assert.False(settings.Protection.RcEnabled); Assert.False(settings.Protection.CrcEnabled);
    }
}

public class StablePayloadContext : CloseContext
{
    private StepExecutionInfo? _stable;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name != "get_CurrentStep") return base.Invoke(method, args);
        var current = (StepExecutionInfo)base.Invoke(method, args)!;
        if (_stable == null) { _stable = current; _stable.Step.StepSetting.StepAddress = "payload-test-stable-step"; }
        else _stable.Step.StepSetting.Setting = current.Step.StepSetting.Setting;
        return _stable;
    }
}
