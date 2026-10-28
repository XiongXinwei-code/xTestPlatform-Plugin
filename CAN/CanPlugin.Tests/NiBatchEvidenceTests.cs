using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using CAN.Adapters.NiXnet;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class NiBatchEvidenceTests
{
    [Theory]
    [InlineData("admission-progress")]
    [InlineData("admission-diagnostic")]
    [InlineData("confirmed-progress")]
    [InlineData("completed-diagnostic")]
    public void Callback_cancellation_is_rechecked_at_native_and_success_boundaries(string boundary)
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2);
        using var cancel = new CancellationTokenSource();
        try
        {
            Assert.ThrowsAny<OperationCanceledException>(() => Send(adapter, Frames, cancel.Token,
                progress: done => { if (boundary == (done ? "confirmed-progress" : "admission-progress")) cancel.Cancel(); },
                publish: value => { if ((boundary == "admission-diagnostic" && value.Phase == "NativeAdmission") ||
                    (boundary == "completed-diagnostic" && value.Phase == "Completed")) cancel.Cancel(); }));
            if (boundary.StartsWith("admission-", StringComparison.Ordinal))
            { Assert.Equal(0, io.Calls); Assert.Equal(0, Ledger(adapter).PendingCount); }
        }
        finally { Close(adapter); }
    }

    private static CanMessage[] Frames => [new() { Id = 0x201, Data = [1, 2, 3, 4] }, new() { Id = 0x80, Data = [0] }];
    private static NiTransmitLedger Ledger(NiXnetAdapter adapter) => (NiTransmitLedger)typeof(NiXnetAdapter)
        .GetField("_transmits", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
    private static void Close(NiXnetAdapter adapter) { try { adapter.Close(); } catch (Exception) when (adapter.HandlesReleased) { } }
    private static void Send(NiXnetAdapter adapter, CanMessage[] frames, CancellationToken ct = default,
        Action<bool>? progress = null, Action<CanBatchDiagnostic>? publish = null, Action? admission = null, int timeout = 5000)
        => ((ICanBudgetedConfirmedBatchTransmitter)adapter).WriteBatchConfirmed(frames, CanConfirmationBudget.Start(timeout, ct),
            ct, admission ?? (() => { }), value =>
            {
                if (value.Kind == CanBatchProgressKind.NativeNotEntered) return;
                for (int i = 0; i < value.Count; i++) progress?.Invoke(value.Kind == CanBatchProgressKind.FrameConfirmed);
            }, publish ?? (_ => { }));

    [Theory]
    [InlineData(4)] // CSP / CSV target width.
    [InlineData(2)] // CST target width.
    public void Whole_batch_is_one_native_call_and_preserves_payload_order_and_evidence(int width)
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2);
        CanBatchDiagnostic? last = null;
        try
        {
            var frames = new[] { new CanMessage { Id = 0x201, Data = Enumerable.Range(1, width).Select(i => (byte)i).ToArray() }, Frames[1] };
            int submitted = 0, confirmed = 0;
            Send(adapter, frames, progress: done => { if (done) confirmed++; else submitted++; }, publish: value => last = value);
            Assert.Equal(1, io.Calls); Assert.Equal(new uint[] { 0x201, 0x80 }, io.Ids);
            Assert.Equal(2, submitted); Assert.Equal(2, confirmed);
            Assert.Equal(width, io.Buffer![15]); Assert.Equal(1, io.Buffer[24 + 15]); Assert.Equal(0, io.Buffer[24 + 16]);
            Assert.Equal("Completed", last!.Phase); Assert.True(last.NativeExited); Assert.Equal(2, last.KnownAcceptedFrames);
            Assert.All(last.Frames, f => { Assert.True(f.NativeSucceeded); Assert.True(f.Confirmed); Assert.True(f.EchoRawTimestamp100ns > f.Barrier100ns); Assert.NotNull(f.EchoObservedMonotonicTicks); });
            Assert.True(io.TimeoutSeconds > 0 && io.TimeoutSeconds <= 5);
        }
        finally { Close(adapter); }
    }

    [Fact]
    public void Raw_batch_reuses_padding_for_zero_Dlc_classic_extended_and_Fd()
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2);
        typeof(NiXnetAdapter).GetField("_protocol", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(adapter, CanProtocolType.FD);
        CanBatchDiagnostic? last = null;
        try
        {
            Send(adapter, [new() { Id = 0x12345, FrameType = CanFrameType.Extended, Data = [] },
                new() { Id = 0x201, Data = Enumerable.Range(0, 9).Select(i => (byte)i).ToArray(), IsFd = true }], publish: value => last = value);
            var frames = NiOrderedBatchTests.BatchDriver.Split(io.Buffer!).ToArray();
            Assert.Equal(24, frames[0].Length); Assert.Equal(0, frames[0][15]); Assert.Equal(32, frames[1].Length);
            Assert.Equal(12, frames[1][15]); Assert.All(frames[1].Skip(25).Take(3), value => Assert.Equal(0, value));
            Assert.Equal("Extended", last!.Frames[0].FrameType); Assert.Equal(12, last.Frames[1].Dlc);
        }
        finally { Close(adapter); }
    }

    [Fact]
    public async Task All_echoes_before_native_return_do_not_confirm_or_release_handles()
    {
        var io = new Driver { BlockNative = true }; var adapter = new NiXnetAdapter(io, 1, 2);
        int confirmed = 0; CanBatchDiagnostic? last = null;
        var sending = Task.Run(() => Send(adapter, Frames, progress: done => { if (done) Interlocked.Increment(ref confirmed); }, publish: value => last = value));
        try
        {
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(sending.IsCompleted); Assert.Equal(0, Volatile.Read(ref confirmed));
            Assert.Equal(2, Ledger(adapter).PendingCount); Assert.False(last!.NativeExited);
            io.Release.Set(); await sending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, confirmed); Assert.True(last!.NativeExited); Assert.True(Ledger(adapter).Quiescent);
        }
        finally { io.Release.Set(); try { await sending; } catch (Exception) { } Close(adapter); }
    }

    [Fact]
    public async Task Echoes_need_session_completion_and_a_missing_Sync_stays_pending_after_cancellation()
    {
        var io = new Driver { SessionDone = false }; var adapter = new NiXnetAdapter(io, 1, 2);
        var sending = Task.Run(() => Send(adapter, Frames));
        try
        {
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await io.QueriedAfterWrite.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(sending.IsCompleted); Assert.False(Ledger(adapter).Quiescent);
            io.SessionDone = true; await sending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { io.SessionDone = true; try { await sending; } finally { Close(adapter); } }

        io = new Driver { EchoCount = 1 }; adapter = new NiXnetAdapter(io, 3, 4);
        using var cancel = new CancellationTokenSource(); CanBatchDiagnostic? last = null;
        sending = Task.Run(() => Send(adapter, Frames, cancel.Token, publish: value => last = value));
        try
        {
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
            Assert.True(last!.NativeExited); Assert.True(last.ConfirmedFrames < 2); Assert.True(Ledger(adapter).PendingCount > 0);
            Assert.Throws<InvalidOperationException>(() => adapter.Write(new() { Id = 0x202, Data = [1] }));
        }
        finally { Close(adapter); }
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(NiXnetApi.nxErrEventTimeout, 0)]
    public void Sdk_failure_does_not_claim_accepted_count_or_confirm_echoes(int status, int? accepted)
    {
        var io = new Driver { Status = status }; var adapter = new NiXnetAdapter(io, 1, 2);
        CanBatchDiagnostic? last = null; int confirmed = 0;
        try
        {
            Assert.Throws<InvalidOperationException>(() => Send(adapter, Frames, progress: done => { if (done) confirmed++; }, publish: value => last = value));
            Assert.Equal(1, io.Calls); Assert.Equal(2, Ledger(adapter).PendingCount); Assert.Equal(0, confirmed);
            Assert.Equal(accepted, last!.KnownAcceptedFrames); Assert.All(last.Frames, frame => Assert.False(frame.NativeSucceeded));
            Assert.False(adapter.ConfirmTransmitQuiescent());
        }
        finally { Close(adapter); }
    }

    [Fact]
    public async Task Cancelled_or_expired_lock_wait_does_not_submit_a_second_batch_or_change_the_first()
    {
        var io = new Driver { BlockNative = true }; var adapter = new NiXnetAdapter(io, 1, 2);
        var first = Task.Run(() => Send(adapter, Frames));
        try
        {
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => Task.Run(() => Send(adapter, Frames, timeout: 50)));
            Assert.Equal(1, io.Calls); Assert.Equal(2, Ledger(adapter).PendingCount);
            io.Release.Set(); await first.WaitAsync(TimeSpan.FromSeconds(5)); Assert.True(Ledger(adapter).Quiescent);
        }
        finally { io.Release.Set(); try { await first; } finally { Close(adapter); } }
    }

    [Fact]
    public void Final_pre_native_cancellation_rolls_back_only_never_submitted_tickets()
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2);
        using var cancel = new CancellationTokenSource();
        try
        {
            Assert.ThrowsAny<OperationCanceledException>(() => Send(adapter, Frames, cancel.Token, admission: () =>
            { if (Ledger(adapter).PendingCount != 0) cancel.Cancel(); cancel.Token.ThrowIfCancellationRequested(); }));
            Assert.Equal(0, io.Calls); Assert.Equal(0, Ledger(adapter).PendingCount);
            Send(adapter, Frames); Assert.Equal(1, io.Calls);
        }
        finally { Close(adapter); }
    }

    [Fact]
    public void All_input_validation_and_batch_capability_checks_precede_native_submission()
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2, orderedBatchAvailable: false);
        try
        {
            Assert.Throws<NotSupportedException>(() => Send(adapter, Frames));
            Assert.Equal(0, io.Calls); Assert.Equal(0, Ledger(adapter).PendingCount);
            adapter.WriteConfirmed(Frames[1], default); Assert.Equal(1, io.SingleCalls); // Single SYNC remains compatible.
        }
        finally { Close(adapter); }
        io = new Driver(); adapter = new NiXnetAdapter(io, 3, 4);
        try
        {
            Assert.Throws<ArgumentException>(() => Send(adapter, [Frames[0], new() { Id = 0xFFF, Data = [1] }]));
            Assert.Equal(0, io.Calls); Assert.Equal(0, Ledger(adapter).PendingCount);
        }
        finally { Close(adapter); }
    }

    [Fact]
    public void Ledger_batch_capacity_failure_is_atomic_and_repeated_echo_does_not_confirm_the_next_ticket()
    {
        var ledger = new NiTransmitLedger(); var epoch = Guid.NewGuid(); ledger.Reset(epoch);
        var tickets = ledger.RegisterBatch([Frames[0], Frames[0]], 100, Guid.NewGuid());
        foreach (var ticket in tickets) ledger.NativeExited(ticket, true);
        CanMessage Echo(ulong time) => new() { Id = 0x201, Data = [1, 2, 3, 4], IsTransmitEcho = true, RawTimestamp100ns = time };
        ledger.Observe(Guid.NewGuid(), Echo(101)); ledger.Observe(epoch, Echo(99)); ledger.SessionTransmitted(); Assert.False(tickets[0].Confirmed);
        ledger.Observe(epoch, Echo(101)); ledger.SessionTransmitted(); Assert.True(tickets[0].Confirmed);
        ledger.Observe(epoch, Echo(101)); ledger.SessionTransmitted(); Assert.False(tickets[1].Confirmed);
        ledger.Observe(epoch, Echo(102)); ledger.SessionTransmitted(); Assert.True(tickets[1].Confirmed);
        ledger.RegisterBatch(Enumerable.Repeat(Frames[0], 8191).ToArray(), 200, Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => ledger.RegisterBatch(Frames, 200, Guid.NewGuid()));
        Assert.Equal(8191, ledger.PendingCount);
    }

    internal sealed class Driver : NiXnetIo
    {
        internal bool BlockNative; internal volatile bool SessionDone = true;
        internal int EchoCount = int.MaxValue, Status, Calls, SingleCalls;
        internal int Cleared;
        internal string? ThrowAfterNative;
        internal double TimeoutSeconds; internal byte[]? Buffer;
        internal readonly List<uint> Ids = [];
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource QueriedAfterWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release = new();
        private long _time = 130000000000000000; private readonly ConcurrentQueue<byte[]> _incoming = new();
        public override int WriteBatch(uint session, byte[] buffer, double timeoutSeconds)
        {
            Calls++; Buffer = buffer.ToArray(); TimeoutSeconds = timeoutSeconds;
            Emit(buffer); Entered.TrySetResult();
            if (BlockNative && !Release.Wait(10000)) throw new TimeoutException("fake native barrier");
            if (ThrowAfterNative != null) throw new InvalidOperationException(ThrowAfterNative);
            return Status;
        }
        public override int Write(uint session, byte[] buffer) { SingleCalls++; Emit(buffer); return Status; }
        private void Emit(byte[] buffer)
        {
            int i = 0;
            foreach (var frame in NiOrderedBatchTests.BatchDriver.Split(buffer))
            {
                Ids.Add(BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8)));
                if (i++ >= EchoCount) continue;
                frame[13] |= 0x80; BinaryPrimitives.WriteUInt64LittleEndian(frame, (ulong)Interlocked.Add(ref _time, 100)); _incoming.Enqueue(frame);
            }
        }
        public override int Read(uint session, byte[] buffer, out uint count)
        { if (_incoming.TryDequeue(out var frame)) { frame.CopyTo(buffer, 0); count = (uint)frame.Length; } else count = 0; return 0; }
        public override int ReadTime(uint session, out ulong time, out int fault) { time = (ulong)Interlocked.Add(ref _time, 100); fault = 0; return 0; }
        public override int TransmitComplete(uint session) { if (Calls > 0) QueriedAfterWrite.TrySetResult(); return Calls == 0 || SessionDone ? 0 : NiXnetApi.nxErrEventTimeout; }
        public override int Stop(uint session) => 0;
        public override int Clear(uint session) { Interlocked.Increment(ref Cleared); return 0; }
        public override void Check(int status) { if (status < 0) throw new InvalidOperationException("fake NI SDK failure " + status); }
    }
}
