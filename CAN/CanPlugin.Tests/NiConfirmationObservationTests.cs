using CAN.Adapters.NiXnet;
using CAN.Models;
using CAN.Runtime;
using System.Reflection;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class NiConfirmationObservationTests
{
    private static CanMessage[] Frames => [new() { Id = 0x201, Data = [1, 2, 3, 4] }, new() { Id = 0x80, Data = [0] }];
    private static void Send(NiXnetAdapter adapter, CancellationToken ct, Action<CanBatchProgress> progress,
        Action<CanBatchDiagnostic> publish) => ((ICanBudgetedConfirmedBatchTransmitter)adapter).WriteBatchConfirmed(
            Frames, CanConfirmationBudget.Start(5000, ct), ct, () => { }, progress, publish);
    private static void Close(NiXnetAdapter adapter) { try { adapter.Close(); } catch (Exception) when (adapter.HandlesReleased) { } }

    [Fact]
    public void Initial_queue_preflight_and_confirmation_have_distinct_query_windows()
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2);
        try
        {
            CanBatchDiagnostic? last = null;
            Send(adapter, default, _ => { }, value => last = value);
            Assert.Equal(2, io.Queries); Assert.Equal(1, last!.PreflightCompletionQueries);
            Assert.Equal(1, last.ConfirmationCompletionQueries);
            Assert.Equal(last.SessionCompletionQueryMs, last.PreflightCompletionQueryMs + last.ConfirmationCompletionQueryMs, 8);
            Assert.True(last.ConfirmationWaitMs >= last.ConfirmationCompletionQueryMs);
        }
        finally { Close(adapter); }
    }

    [Fact]
    public void Cancelled_preflight_preserves_original_cancellation_even_when_sdk_returns_incomplete()
    {
        using var cancel = new CancellationTokenSource();
        var io = new Driver { BeforeQuery = () => cancel.Cancel(), QueryStatusOverride = NiXnetApi.nxErrEventTimeout };
        var adapter = new NiXnetAdapter(io, 1, 2);
        try
        {
            Assert.ThrowsAny<OperationCanceledException>(() => Send(adapter, cancel.Token, _ => { }, _ => { }));
            Assert.Equal(0, io.Inner.Calls);
        }
        finally { io.BeforeQuery = null; io.QueryStatusOverride = null; Close(adapter); }
    }

    [Fact]
    public void Already_preflighted_empty_session_needs_only_one_post_echo_completion_query()
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2);
        try
        {
            adapter.VerifyConfirmationAvailable(); io.Queries = 0;
            CanBatchDiagnostic? last = null;
            Send(adapter, default, _ => { }, value => last = value);
            Assert.Equal(1, io.Queries);
            Assert.Equal(1, last!.SessionCompletionQueries);
            Assert.Equal(0, last.PreflightCompletionQueries);
            Assert.Equal(1, last.ConfirmationCompletionQueries);
            Assert.Equal(last.SessionCompletionQueryMs, last.ConfirmationCompletionQueryMs);
            Assert.True(last.ConfirmationWaitMs >= last.ConfirmationCompletionQueryMs);
            Assert.Equal(2, last.ReportedConfirmedFrames);
            Assert.All(last.Frames, frame => Assert.True(frame.Confirmed));
        }
        finally { io.QueryAfterWrite = null; Close(adapter); }
    }

    [Fact]
    public async Task Delayed_session_query_has_separate_write_exit_task_exit_and_historical_deadline_evidence()
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2);
        var owner = new CanConnectionResource(adapter);
        var service = owner.StartService("query", [new("feedback", 0x181, 1, 1000)]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(); int blocked = 0;
        io.QueryAfterWrite = () =>
        {
            if (Interlocked.Exchange(ref blocked, 1) != 0) return;
            entered.TrySetResult();
            if (!release.Wait(10000)) throw new TimeoutException("fake query release not provided");
        };
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10, 1000, 1000));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var pending = service.Snapshot().Cycle;
            Assert.False(pending.HostExited); Assert.False(pending.OwnedSendTaskExited);
            Assert.Null(pending.OwnedTxQuiescent); Assert.Equal("SendTaskPending", pending.OwnedTxQuiescenceEvidence);
            Assert.True(pending.Timing!.Batch!.NativeExited); // SDK write exited, completion query has not.
            using var guard = new CancellationTokenSource(5000);
            while (service.Snapshot().Cycle.Timing?.DeadlineObservedWithPendingSendTask != true) await Task.Delay(2, guard.Token);
            Assert.Null(owner.IsolationReason);
            Assert.True(service.Snapshot().Cycle.Timing!.DeadlineObservedWithPendingSendTask);
            Assert.False(service.Snapshot().Cycle.Timing!.SendTaskExited);
            release.Set();
            while (!service.Snapshot().Cycle.HostExited) await Task.Delay(2, guard.Token);
            var exited = service.Snapshot().Cycle;
            Assert.True(exited.OwnedSendTaskExited); Assert.True(exited.Timing!.SendTaskExited);
            Assert.True(exited.Timing.DeadlineObservedWithPendingSendTask);
            Assert.True(exited.Timing.Batch!.NativeExited);
            // The deadline remains a service fault; fully known sends do not poison SDO.
            Assert.Null(owner.IsolationReason); Assert.NotNull(exited.Error);
            try { await owner.CloseAsync(5000); } catch (Exception) when (owner.IsClosed) { }
            Assert.True(owner.IsClosed);
        }
        finally { release.Set(); io.QueryAfterWrite = null; try { await owner.CloseAsync(5000); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task No_completion_queries_are_made_while_batch_echoes_are_missing()
    {
        var io = new Driver { AllowEcho = false }; var adapter = new NiXnetAdapter(io, 1, 2);
        using var cancel = new CancellationTokenSource();
        Task? sending = null;
        try
        {
            adapter.VerifyConfirmationAvailable(); io.Queries = 0;
            sending = Task.Run(() => Send(adapter, cancel.Token, _ => { }, _ => { }));
            await io.Inner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // This checks an absence across a controlled fake gate, not bus timing.
            await Task.Delay(100);
            Assert.Equal(0, Volatile.Read(ref io.Queries));
            io.AllowEcho = true;
            await sending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, io.Queries);
        }
        finally { io.AllowEcho = true; cancel.Cancel(); if (sending != null) try { await sending; } catch (OperationCanceledException) { } Close(adapter); }
    }

    [Fact]
    public void Cancellation_after_successful_session_query_records_real_confirmation_without_reporting_success()
    {
        using var cancel = new CancellationTokenSource();
        var io = new Driver();
        var adapter = new NiXnetAdapter(io, 1, 2);
        io.QueryAfterWrite = () =>
        {
            // Let the independent pump finish echo association while the fake
            // SDK query is blocked, then cancel immediately before it returns.
            var ledger = (NiTransmitLedger)typeof(NiXnetAdapter).GetField("_transmits", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
            var gate = typeof(NiTransmitLedger).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ledger)!;
            var pending = (Queue<NiTransmitLedger.Ticket>)typeof(NiTransmitLedger).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ledger)!;
            Assert.True(SpinWait.SpinUntil(() => { lock (gate) return pending.Count == 2 && pending.All(ticket => ticket.EchoSeen && ticket.NativeDone && ticket.NativeSuccess); }, 5000));
            cancel.Cancel();
        };
        CanBatchDiagnostic? last = null; int reported = 0;
        try
        {
            adapter.VerifyConfirmationAvailable();
            Assert.ThrowsAny<OperationCanceledException>(() => Send(adapter, cancel.Token,
                progress => { if (progress.Kind == CanBatchProgressKind.FrameConfirmed) reported += progress.Count; }, value => last = value));
            Assert.All(last!.Frames, frame => Assert.True(frame.Confirmed));
            Assert.Equal(2, last.ConfirmedFrames);
            Assert.Equal(2, reported);
            // A delivery cancellation does not undo known evidence or make a known send unknown.
            Assert.True(adapter.ConfirmationAvailable);
            io.QueryAfterWrite = null;
            Assert.True(adapter.ConfirmTransmitQuiescent());
        }
        finally { Close(adapter); }
    }

    private sealed class Driver : NiXnetIo
    {
        internal readonly NiBatchEvidenceTests.Driver Inner = new();
        internal int Queries;
        internal volatile bool AllowEcho = true;
        internal Action? QueryAfterWrite;
        internal Action? BeforeQuery;
        internal int? QueryStatusOverride;
        public override int WriteBatch(uint session, byte[] buffer, double timeoutSeconds) => Inner.WriteBatch(session, buffer, timeoutSeconds);
        public override int Write(uint session, byte[] buffer) => Inner.Write(session, buffer);
        public override int Read(uint session, byte[] buffer, out uint count)
        { if (AllowEcho) return Inner.Read(session, buffer, out count); count = 0; return 0; }
        public override int ReadTime(uint session, out ulong time, out int fault) => Inner.ReadTime(session, out time, out fault);
        public override int TransmitComplete(uint session)
        { Interlocked.Increment(ref Queries); BeforeQuery?.Invoke(); if (Inner.Calls > 0) QueryAfterWrite?.Invoke(); return QueryStatusOverride ?? Inner.TransmitComplete(session); }
        public override int Stop(uint session) => Inner.Stop(session);
        public override int Clear(uint session) => Inner.Clear(session);
        public override void Check(int status) => Inner.Check(status);
    }
}
