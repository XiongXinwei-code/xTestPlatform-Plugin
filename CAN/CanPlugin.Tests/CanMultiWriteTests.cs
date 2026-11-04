using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using CAN.Models;
using CAN.Runtime;
using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanMultiWriteTests
{
    private static CanPayloadProtectionSetting Counter(uint initial, int at = 0, long step = 1) => new()
    { RcEnabled = true, RcStartByte = at, RcEndByte = at, RcInitialValue = initial, RcStep = step };

    private static CanPayloadProtectionSetting Crc8Counter() => new()
    {
        RcEnabled = true, RcInitialValue = 1, CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc8,
        CrcStartByte = 3, CrcEndByte = 3, CrcDataStartByte = 0, CrcDataEndByte = 2
    };
    private static CanPayloadProtectionSetting ModbusCounter() => new()
    {
        RcEnabled = true, RcStartByte = 1, RcEndByte = 1, RcInitialValue = 10, RcStep = 2,
        CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc16Modbus,
        CrcStartByte = 3, CrcEndByte = 4, CrcDataStartByte = 0, CrcDataEndByte = 2
    };
    private static CanMessage Frame(uint id, byte[]? data = null, bool fd = false) => new() { Id = id, Data = data ?? [0, 0x55], IsFd = fd };
    private static ProtectedCanFrame Row(uint id, uint initial, int row) => new(Frame(id), Counter(initial), $"test-step/list/{row}", row);
    private static CanSubmissionGate Queue(CanConnectionResource owner) => (CanSubmissionGate)typeof(CanConnectionResource)
        .GetField("_txGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static async Task Until(Func<bool> condition)
    {
        using var stop = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(5, stop.Token);
    }
    private static void Completed(ExecutionResult result) => Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());

    [Fact]
    public async Task Freeze_after_final_check_before_admission_prevents_old_attempt()
    {
        var progress = new CanConnectionResource.MultiWriteProgress();
        using var resume = new ManualResetEventSlim();
        var checkedBeforeAdmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int committed = 0;
        var actual = Task.Run(() =>
        {
            checkedBeforeAdmission.SetResult(); resume.Wait();
            progress.EnterAttempt(() => { });
            Interlocked.Increment(ref committed); progress.ExitAttempt();
        });
        try
        {
            await checkedBeforeAdmission.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(progress.Freeze()); resume.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => actual);
            Assert.Equal(0, committed); Assert.Equal(0, progress.Attempted);
        }
        finally { resume.Set(); }
    }

    [Fact]
    public void Admission_before_freeze_retains_pending_ownership_until_exit_and_rejects_later_attempts()
    {
        var progress = new CanConnectionResource.MultiWriteProgress();
        progress.EnterAttempt(() => { });
        Assert.True(progress.Freeze()); Assert.Equal(1, progress.NativeActive);
        Assert.ThrowsAny<OperationCanceledException>(() => progress.EnterAttempt(() => { }));
        progress.ExitAttempt(); Assert.False(progress.Freeze());
        Assert.Equal(1, progress.Attempted); Assert.Equal(0, progress.NativeActive);
    }
    private static (IExecutionContext Context, StablePayloadContext State) Context(CanConnectionResource owner, object setting, bool cyclic = false)
    {
        var context = DispatchProxy.Create<IExecutionContext, StablePayloadContext>(); var state = (StablePayloadContext)(object)context;
        state.Adapter = owner; state.Plugin = cyclic ? new CanCyclicSendStartPlugin() : new CanWritePlugin(); state.Setting = setting;
        return (context, state);
    }

    [Fact]
    public async Task Batch_preserves_order_and_each_row_has_independent_RC_CRC_and_payload_copy()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var classic = Frame(0x123, [0, 0xA1, 0xB2, 0]); var fd = Frame(0x124, [0x11, 0, 0x22, 0, 0], true);
        var frames = new[] { new ProtectedCanFrame(classic, Crc8Counter(), "A", 1), new ProtectedCanFrame(fd, ModbusCounter(), "B", 2) };
        try
        {
            for (int i = 0; i < 3; i++)
            {
                var result = await owner.WriteProtectedBatchAsync(frames, 2000);
                Assert.Equal(new uint[] { 0x123, 0x124 }, result.Select(f => f.Id));
                Assert.Equal((byte)(1 + i), result[0].Data[0]); Assert.Equal((byte)(10 + i * 2), result[1].Data[1]);
                Assert.False(result[0].IsFd); Assert.True(result[1].IsFd);
                VerifyProtection(result[0]); VerifyProtection(result[1]);
            }
            Assert.Equal(new byte[] { 0, 0xA1, 0xB2, 0 }, classic.Data);
            Assert.Equal(new byte[] { 0x11, 0, 0x22, 0, 0 }, fd.Data);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Entire_batch_is_validated_before_first_submission()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.Classic });
        try
        {
            var invalidId = new ProtectedCanFrame(Frame(0x800), new(), "bad", 2);
            await Assert.ThrowsAnyAsync<Exception>(() => owner.WriteProtectedBatchAsync([Row(0x123, 4, 1), invalidId], 1000));
            var invalidRange = new ProtectedCanFrame(Frame(0x124), new() { CrcEnabled = true, CrcStartByte = 5, CrcEndByte = 5 }, "bad", 2);
            await Assert.ThrowsAnyAsync<Exception>(() => owner.WriteProtectedBatchAsync([Row(0x123, 4, 1), invalidRange], 1000));
            var incompatibleFd = new ProtectedCanFrame(Frame(0x124, new byte[12], true), new(), "bad", 2);
            await Assert.ThrowsAnyAsync<Exception>(() => owner.WriteProtectedBatchAsync([Row(0x123, 4, 1), incompatibleFd], 1000));
            Assert.Empty(raw.Sent);
            await owner.WriteProtectedBatchAsync([Row(0x123, 4, 1)], 1000);
            Assert.Equal(4, Assert.Single(raw.Sent).Data[0]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Queued_cancellation_does_not_send_or_advance_any_row()
    {
        using var release = new ManualResetEventSlim(); using var stop = new CancellationTokenSource();
        var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        var blocking = Task.Run(() => owner.Write(Frame(0x111)));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var rows = new[] { Row(0x123, 4, 1), Row(0x124, 8, 2) };
            var batch = owner.WriteProtectedBatchAsync(rows, 5000, stop.Token); await Until(() => Queue(owner).QueuedCount > 0);
            stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => batch);
            release.Set(); await blocking;
            await owner.WriteProtectedBatchAsync(rows, 1000);
            Assert.Equal(new uint[] { 0x111, 0x123, 0x124 }, raw.Sent.Select(f => f.Id));
            Assert.Equal(4, raw.Sent.ElementAt(1).Data[0]); Assert.Equal(8, raw.Sent.ElementAt(2).Data[0]);
        }
        finally { release.Set(); await blocking; await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_final_admission_does_not_replace_or_remove_existing_counter(bool disableCounter)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        const string senderKey = "cancelled-protection-change";
        var original = Counter(3);
        try
        {
            await owner.WriteProtectedAsync(Frame(0x123), original, senderKey, 1000);
            Assert.Equal(3, Assert.Single(raw.Sent).Data[0]);

            var replacement = disableCounter ? new CanPayloadProtectionSetting() : Counter(99);
            var write = typeof(CanConnectionResource).GetMethod("WriteUnderSubmissionGate", BindingFlags.Instance | BindingFlags.NonPublic)!;
            using (Queue(owner).Enter(null, default, () => { }, 0x123))
            {
                var error = Assert.Throws<TargetInvocationException>(() => write.Invoke(owner,
                    [Frame(0x123), (Action)(() => { }), null, null, replacement, senderKey,
                     (Action)(() => throw new OperationCanceledException("Cancelled at final admission")), null, null, null]));
                Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
            }

            Assert.Single(raw.Sent);
            await owner.WriteProtectedAsync(Frame(0x123), original, senderKey, 1000);
            Assert.Equal(new byte[] { 3, 4 }, raw.Sent.Select(frame => frame.Data[0]));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Queued_batch_captures_data_and_protection_before_caller_mutation()
    {
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        var blocking = Task.Run(() => owner.Write(Frame(0x111)));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var frame = Frame(0x123); var protection = Counter(5);
            var batch = owner.WriteProtectedBatchAsync([new(frame, protection, "frozen", 1)], 5000);
            await Until(() => Queue(owner).QueuedCount > 0);
            frame.Data[1] = 0xCC; protection.RcInitialValue = 99; protection.RcStep = 20;
            release.Set(); await blocking; await batch;
            Assert.Equal(new byte[] { 5, 0x55 }, Assert.Single(raw.Sent, f => f.Id == 0x123).Data);
        }
        finally { release.Set(); await blocking; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Ordinary_writer_cannot_split_an_admitted_batch_even_with_higher_priority_ID()
    {
        using var release = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new FakeAdapter { OnWrite = frame => { if (frame.Id == 0x500) { entered.TrySetResult(); release.Wait(); } } };
        var owner = new CanConnectionResource(raw); Task? outsider = null;
        var batch = owner.WriteProtectedBatchAsync([Row(0x500, 1, 1), Row(0x501, 2, 2)], 5000);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); outsider = owner.WriteAsync(Frame(0x100), 5000);
            await Until(() => Queue(owner).QueuedCount > 0); release.Set(); await batch; await outsider;
            Assert.Equal(new uint[] { 0x500, 0x501, 0x100 }, raw.Sent.Select(f => f.Id));
        }
        finally { release.Set(); await batch; if (outsider != null) await outsider; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Partial_native_failure_keeps_attempted_counters_and_does_not_send_remaining_row()
    {
        var attempts = new ConcurrentQueue<(uint Id, byte Counter)>(); bool fail = true;
        var raw = new FakeAdapter { OnWrite = frame =>
        {
            attempts.Enqueue((frame.Id, frame.Data[0]));
            if (frame.Id == 0x124 && fail) throw new InvalidOperationException("native acceptance unknown");
        } };
        var owner = new CanConnectionResource(raw); var rows = new[] { Row(0x123, 3, 1), Row(0x124, 9, 2), Row(0x125, 20, 3) };
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => owner.WriteProtectedBatchAsync(rows, 2000));
            Assert.Contains("native acceptance unknown", error.ToString());
            Assert.Equal(new uint[] { 0x123, 0x124 }, attempts.Select(x => x.Id));
            Assert.Single(raw.Sent); fail = false;
            var next = await owner.WriteProtectedBatchAsync(rows, 2000);
            Assert.Equal(new byte[] { 4, 10, 20 }, next.Select(f => f.Data[0]));
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interrupted_native_batch_retains_owner_and_never_sends_later_rows(bool cancel)
    {
        using var release = new ManualResetEventSlim(); using var stop = new CancellationTokenSource();
        var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        var batch = owner.WriteProtectedBatchAsync([Row(0x123, 7, 1), Row(0x124, 12, 2)], cancel ? 5000 : 250, stop.Token);
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel) { stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => batch); }
            else await Assert.ThrowsAsync<TimeoutException>(() => batch);
            Assert.NotNull(owner.IsolationReason);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount);
        }
        finally { release.Set(); await owner.CloseAsync(); }
        Assert.Equal(0x123u, Assert.Single(raw.Sent).Id); Assert.True(owner.IsClosed); Assert.Equal(1, raw.CloseCount);
        var freshRaw = new FakeAdapter(); var fresh = new CanConnectionResource(freshRaw);
        try { await fresh.WriteProtectedBatchAsync([Row(0x123, 7, 1), Row(0x124, 12, 2)], 1000); Assert.Equal(new byte[] { 7, 12 }, freshRaw.Sent.Select(f => f.Data[0])); }
        finally { await fresh.CloseAsync(); }
    }

    [Fact]
    public async Task Executor_ignores_disabled_invalid_rows_and_legacy_fields_when_list_is_enabled()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var setting = new CanWriteSetting { UseMessageList = true, CanId = "INVALID_OLD_ID", Data = "INVALID_OLD_DATA", Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 A1 B2 00\"", Protection = Crc8Counter() },
            new() { Enabled = false, CanId = "INVALID_EXPRESSION", Data = "\"0\"", Protection = new() { CrcEnabled = true, CrcStartByte = 90 } },
            new() { CanId = "\"0x124\"", Data = "\"11 00 22 00 00\"", UseFdFrame = true, Protection = ModbusCounter() }
        ] };
        var (context, state) = Context(owner, setting);
        try
        {
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            Assert.Equal(new uint[] { 0x123, 0x124 }, raw.Sent.Select(f => f.Id));
            Assert.All(raw.Sent, VerifyProtection);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Executor_prepares_all_rows_before_sending_and_same_ID_rows_keep_independent_counters()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new CanWriteSetting { UseMessageList = true, Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 55\"", Protection = Counter(3) },
            new() { CanId = "\"0x123\"", Data = "INVALID_EXPRESSION", Protection = Counter(9) }
        ] };
        var (context, state) = Context(owner, setting);
        try
        {
            Assert.Equal(TestStatus.Error, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status); Assert.Empty(raw.Sent);
            setting.Messages[1].Data = "\"00 55\"";
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            Assert.Equal(new byte[] { 3, 9, 4, 10 }, raw.Sent.Select(f => f.Data[0]));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Legacy_map_defaults_to_single_frame_and_saved_list_does_not_change_that()
    {
        var plugin = new CanWritePlugin(); var setting = (CanWriteSetting)plugin.CreateSerializer().Deserialize(LegacySetting(), 1);
        Assert.False(setting.UseMessageList); Assert.Empty(setting.Messages);
        setting.Messages.Add(new() { CanId = "INVALID_EXPRESSION", Data = "INVALID_EXPRESSION" });
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); var (context, state) = Context(owner, setting);
        try { Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context)); Assert.Equal(0x222u, Assert.Single(raw.Sent).Id); Assert.Equal(new byte[] { 0xAA, 0xBB }, raw.Sent.Single().Data); }
        finally { await owner.CloseAsync(); }
    }

    private static byte[] LegacySetting()
    {
        var buffer = new ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer);
        writer.WriteMapHeader(2); writer.Write("CanId"); writer.Write("\"0x222\""); writer.Write("Data"); writer.Write("\"AA BB\""); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    [Fact]
    public async Task Cyclic_different_IDs_keep_different_RC_positions_steps_and_CRC_algorithms_after_restart()
    {
        var setting = new CanCyclicSendStartSetting { Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 A1 B2 00\"", CycleTimeMs = 5, Protection = Crc8Counter() },
            new() { CanId = "\"0x124\"", Data = "\"11 00 22 00 00\"", CycleTimeMs = 8, UseFdFrame = true, Protection = ModbusCounter() }
        ] };
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var (context, state) = Context(owner, setting, cyclic: true);
        try
        {
            for (int restart = 0; restart < 2; restart++)
            {
                int offset = raw.Sent.Count; Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
                await Until(() => raw.Sent.Skip(offset).Count(f => f.Id == 0x123) >= 3 && raw.Sent.Skip(offset).Count(f => f.Id == 0x124) >= 3);
                await owner.StopJobAsync("CyclicTask1", 1000, default);
                var sent = raw.Sent.Skip(offset).ToArray(); Assert.All(sent, VerifyProtection);
                Assert.Equal(new byte[] { 1, 2, 3 }, sent.Where(f => f.Id == 0x123).Take(3).Select(f => f.Data[0]));
                Assert.Equal(new byte[] { 10, 12, 14 }, sent.Where(f => f.Id == 0x124).Take(3).Select(f => f.Data[1]));
                int stopped = raw.Sent.Count; await Task.Delay(30); Assert.Equal(stopped, raw.Sent.Count);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    // Independent reference implementations: expected bytes do not call the production protection processor.
    private static void VerifyProtection(CanMessage frame)
    {
        if (frame.Id == 0x123)
        {
            byte crc = 0;
            foreach (byte value in frame.Data.Take(3))
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++) crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 7 : crc << 1);
            }
            Assert.Equal(crc, frame.Data[3]); Assert.Equal(new byte[] { 0xA1, 0xB2 }, frame.Data.Skip(1).Take(2));
        }
        else
        {
            Assert.Equal(0x124u, frame.Id); ushort crc = 0xFFFF;
            foreach (byte value in frame.Data.Take(3))
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++) crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
            }
            Assert.Equal((byte)crc, frame.Data[3]); Assert.Equal((byte)(crc >> 8), frame.Data[4]);
            Assert.Equal(0x11, frame.Data[0]); Assert.Equal(0x22, frame.Data[2]);
        }
    }
}
