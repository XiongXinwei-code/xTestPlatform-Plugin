using System.Diagnostics;
using CAN.Models;
using CAN.Runtime;

namespace CAN.Adapters.NiXnet;

public sealed partial class NiXnetAdapter
{
    private bool _orderedBatchAvailable;
    private int _batchOrderStatus;
    bool ICanBudgetedConfirmedBatchTransmitter.OrderedBatchAvailable => _orderedBatchAvailable;

    void ICanBudgetedConfirmedBatchTransmitter.VerifyOrderedBatchAvailable()
    {
        if (!_orderedBatchAvailable)
            throw new NotSupportedException($"UnsupportedGuarantee：NI 未在接口启动前确认 AsSubmitted 顺序，批次未提交；状态=0x{_batchOrderStatus:X8}");
    }

    void ICanBudgetedConfirmedBatchTransmitter.WriteBatchConfirmed(CanMessage[] frames, CanConfirmationBudget budget,
        CancellationToken ct, Action checkAdmission, Action<CanBatchProgress> progress, Action<CanBatchDiagnostic> publish)
    {
        var id = Guid.NewGuid();
        string phase = "Validation", submission = "NotAttempted";
        NiTransmitLedger.Ticket[] tickets = [];
        bool entered = false, attempted = false, nativeExited = false, admissionReported = false;
        int? accepted = null;
        int queries = 0, reported = 0, preflightQueries = 0, confirmationQueries = 0;
        double queryMs = 0, preflightQueryMs = 0, confirmationQueryMs = 0;
        double? adapterLockMs = null, nativeMs = null, confirmationMs = null;
        int priorPendingAtEntry = 0; bool priorDrainStarted = false, priorDrained = false;
        double? priorDrainMs = null;
        long? nativeStart = null, nativeEnd = null;
        Exception? error = null;
        var nativeClock = new Stopwatch(); var confirmationClock = new Stopwatch();
        var reportedTickets = new bool[frames.Length];
        CanBatchDiagnostic Diagnostic()
        {
            var evidence = _transmits.Evidence(tickets);
            return new(id, "NiXnetOrderedRawBatch", frames.Length, phase)
            {
            AdapterEpoch = _epoch, AdapterLockWaitMs = adapterLockMs,
            NativeStartedMonotonicTicks = nativeStart, NativeReturnedMonotonicTicks = nativeEnd,
            NativeSubmissionMs = nativeMs, ConfirmationWaitMs = confirmationMs,
            NativeCallCount = attempted ? 1 : 0, AttemptedFrames = attempted ? frames.Length : 0,
            KnownAcceptedFrames = attempted ? accepted : 0, NativeExited = attempted ? nativeExited : null,
            SubmissionState = submission, ConfirmedFrames = evidence.Count(frame => frame.Confirmed), ReportedConfirmedFrames = reported,
            SessionCompletionQueries = queries, SessionCompletionQueryMs = queryMs,
            PreflightCompletionQueries = preflightQueries, PreflightCompletionQueryMs = preflightQueryMs,
            ConfirmationCompletionQueries = confirmationQueries, ConfirmationCompletionQueryMs = confirmationQueryMs,
            PriorPendingAtEntry = priorPendingAtEntry, UnresolvedPriorFrames = priorDrainStarted && !priorDrained ? _transmits.PendingCount : 0,
            UnresolvedPriorQueue = priorDrainStarted && !priorDrained && _unknownSend,
            PriorTransmitDrainMs = priorDrainMs,
            Error = error?.ToString(), Frames = evidence
            };
        }
        bool Refresh(bool preflight)
        {
            var start = Stopwatch.GetTimestamp(); queries++;
            if (preflight) preflightQueries++; else confirmationQueries++;
            try { return RefreshTransmitCompletion(); }
            finally
            {
                double duration = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                queryMs += duration;
                if (preflight) preflightQueryMs += duration; else confirmationQueryMs += duration;
            }
        }
        try
        {
            if (frames.Length is < 1 or > 129) throw new ArgumentException("NI 批次要求1~129帧");
            // Validate and normalize every member before any ticket/native call.
            var raw = new byte[frames.Length][]; var normalized = new CanMessage[frames.Length]; int size = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                CAN.Helpers.CanHelper.ValidateTransmitFrame(frames[i]);
                var copy = CanReceiveRouter.Copy(frames[i]);
                CAN.Helpers.CanHelper.ValidateTransmitFrame(copy);
                raw[i] = BuildFrameBytes(copy); size = checked(size + raw[i].Length);
                normalized[i] = ParseFrames(raw[i], raw[i].Length).Single();
            }
            var buffer = new byte[size]; int offset = 0;
            foreach (var bytes in raw) { bytes.CopyTo(buffer, offset); offset += bytes.Length; }
            phase = "AdapterLock";
            long lockStarted = Stopwatch.GetTimestamp();
            budget.Enter(_transmitGate, ct, checkAdmission); entered = true;
            adapterLockMs = Stopwatch.GetElapsedTime(lockStarted).TotalMilliseconds;
            checkAdmission(); _ = budget.Remaining(ct);
            if (frames.Length > 1) ((ICanBudgetedConfirmedBatchTransmitter)this).VerifyOrderedBatchAvailable();
            if (!ConfirmationAvailable) throw new NotSupportedException("NI 发送确认不可用或旧发送未知，未提交本批");
            ThrowIfReceivePumpFailed();
            // Strict mode's initial queue check also covers untracked ordinary
            // writes made before strict mode. Afterwards an empty ledger needs
            // no repeated SDK queue query on every cycle.
            priorPendingAtEntry = _transmits.PendingCount;
            if (!_strictEnabled || _hasUntrackedSubmissions || priorPendingAtEntry != 0)
            {
                phase = "PriorTransmitDrain"; priorDrainStarted = true;
                var drainClock = Stopwatch.StartNew();
                try { DrainPriorTransmits(budget, ct, !_strictEnabled || _hasUntrackedSubmissions, () => Refresh(preflight: true)); priorDrained = true; }
                finally { priorDrainMs = drainClock.Elapsed.TotalMilliseconds; }
                _strictEnabled = true;
            }
            var barrier = ReadInterfaceTime();
            checkAdmission(); _ = budget.Remaining(ct);
            phase = "Register"; tickets = _transmits.RegisterBatch(normalized, barrier, id);
            // Last admission fence. Any failure here rolls back only never-submitted tickets.
            checkAdmission(); _ = budget.Remaining(ct);
            phase = "NativeAdmission";
            admissionReported = true; progress(new(CanBatchProgressKind.NativeAdmission, tickets.Length));
            publish(Diagnostic() with { NativeExited = false, SubmissionState = "EnteringSdk" });
            // Progress/diagnostic callbacks can wait for managed locks. Recheck
            // after them, immediately before SDK entry, using the same budget.
            checkAdmission(); int remaining = budget.Remaining(ct);
            attempted = true; submission = "AcceptanceUnknown"; nativeStart = Stopwatch.GetTimestamp(); nativeClock.Start(); phase = "NativeWrite";
            bool success = false;
            try
            {
                int status = _io.WriteBatch(_txSession, buffer, remaining / 1000d);
                // NI explicitly guarantees none queued for its timeout error only.
                if (status == NiXnetApi.nxErrEventTimeout) { accepted = 0; submission = "NoneQueuedSdkTimeout"; }
                _io.Check(status); success = true; accepted = frames.Length; submission = "AcceptedBySdk";
            }
            finally
            {
                nativeExited = true; nativeEnd = Stopwatch.GetTimestamp(); nativeClock.Stop(); nativeMs = nativeClock.Elapsed.TotalMilliseconds;
                foreach (var ticket in tickets) _transmits.NativeExited(ticket, success);
            }
            // Keep buffer and all tickets owned until the actual native call exits.
            GC.KeepAlive(buffer);
            phase = "ConfirmationWait"; confirmationClock.Start(); publish(Diagnostic());
            while (true)
            {
                long version = _transmits.Version;
                _ = budget.Remaining(ct); ThrowIfReceivePumpFailed();
                // Querying before all required echoes arrive cannot confirm this
                // batch. Echo/native evidence is necessary but never sufficient.
                if (_transmits.ReadyForSessionQuery(tickets)) Refresh(preflight: false);
                for (int i = 0; i < tickets.Length; i++)
                    if (!reportedTickets[i] && _transmits.IsConfirmed(tickets[i]))
                    { progress(new(CanBatchProgressKind.FrameConfirmed, 1)); reportedTickets[i] = true; reported++; }
                // Preserve real confirmations even when a synchronous SDK query
                // returns after the deadline. This still fails the original call.
                _ = budget.Remaining(ct);
                if (reported == tickets.Length) break;
                // Echo/fault pulses wake immediately; bounded fallback rechecks session completion.
                _transmits.WaitForChange(version, Math.Min(2, budget.Remaining(ct)));
            }
            confirmationClock.Stop(); confirmationMs = confirmationClock.Elapsed.TotalMilliseconds;
            _ = budget.Remaining(ct);
            phase = "Completed";
        }
        catch (Exception ex)
        {
            error = ex;
            if (priorDrainStarted && !priorDrained && !_transmits.Quiescent) _unknownSend = true;
            if (attempted)
            {
                if (!_transmits.Quiescent) _unknownSend = true;
            }
            else if (tickets.Length != 0)
            {
                try
                {
                    _transmits.RollbackUnsubmitted(tickets);
                    if (admissionReported) progress(new(CanBatchProgressKind.NativeNotEntered, tickets.Length));
                }
                catch (Exception cleanup) { error = new AggregateException("NI 未提交批次及其登记回收均失败", ex, cleanup); }
            }
        }
        finally
        {
            if (confirmationClock.IsRunning) confirmationMs = confirmationClock.Elapsed.TotalMilliseconds;
            try
            {
                try { publish(Diagnostic()); }
                catch (Exception diagnosticError)
                {
                    error = error == null ? diagnosticError : new AggregateException("NI 批次与诊断回调均失败", error, diagnosticError);
                    if (attempted && !_transmits.Quiescent) _unknownSend = true;
                }
                if (error == null)
                    try { _ = budget.Remaining(ct); }
                    catch (Exception resultError)
                    {
                        error = resultError; phase = "ResultFence"; if (attempted && !_transmits.Quiescent) _unknownSend = true;
                        try { publish(Diagnostic()); }
                        catch (Exception diagnosticError) { error = new AggregateException("NI 结果期限与诊断回调均失败", resultError, diagnosticError); }
                    }
            }
            finally { if (entered) Monitor.Exit(_transmitGate); }
        }
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
