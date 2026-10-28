using System.Diagnostics;
using CAN.Models;
using CAN.Runtime;

namespace CAN.Adapters.NiXnet;

public sealed partial class NiXnetAdapter
{
    void ICanBudgetedBatchSubmitter.VerifySubmissionAvailable()
    {
        if (!_isConnected || _closing || _unknownSend)
            throw new InvalidOperationException("NI 通道未打开、正在关闭或旧原生发送结果未知");
        if (!_orderedBatchAvailable)
            throw new NotSupportedException($"NI 未配置 AsSubmitted，不能保证提交FIFO；状态=0x{_batchOrderStatus:X8}");
    }

    void ICanBudgetedBatchSubmitter.WriteBatchSubmitted(CanMessage[] frames, CanConfirmationBudget budget,
        CancellationToken ct, Action checkAdmission, Action<CanBatchProgress> progress, Action<CanBatchDiagnostic> publish)
    {
        budget = budget with { Operation = "发送提交" };
        var id = Guid.NewGuid();
        bool entered = false, admitted = false, attempted = false, exited = false, returned = false;
        int? accepted = null;
        long? nativeStart = null, nativeEnd = null;
        double? lockMs = null;
        string phase = "Validation", state = "NotAttempted";
        Exception? error = null;
        CanMessage[] copies = [];
        CanBatchDiagnostic Evidence() => new(id, "NiXnetOrderedSubmissionOnly", frames.Length, phase)
        {
            AdapterEpoch = _epoch, AdapterLockWaitMs = lockMs,
            NativeStartedMonotonicTicks = nativeStart, NativeReturnedMonotonicTicks = nativeEnd,
            NativeSubmissionMs = nativeStart is long start && nativeEnd is long end ? Stopwatch.GetElapsedTime(start, end).TotalMilliseconds : null,
            NativeCallCount = attempted ? 1 : 0, AttemptedFrames = attempted ? frames.Length : 0,
            KnownAcceptedFrames = attempted ? accepted : 0, NativeExited = attempted ? exited : null,
            SubmissionState = state, ConfirmedFrames = 0, Error = error?.ToString(),
            AdapterCallExited = attempted ? exited : null,
            Submissions = !attempted ? [] : copies.Select((frame, index) => new CanAdapterSubmissionEvidence(index,
                frame.Id, frame.FrameType.ToString(), frame.IsFd, frame.Dlc, Convert.ToHexString(frame.Data),
                "nxWriteFrame(batch)", nativeStart!.Value, nativeEnd, exited ? returned : null, false, error?.ToString())).ToArray()
        };
        try
        {
            if (frames.Length is < 1 or > 129) throw new ArgumentException("NI 提交批次要求1~129帧");
            copies = frames.Select(CanReceiveRouter.Copy).ToArray();
            foreach (var frame in copies) CAN.Helpers.CanHelper.ValidateTransmitFrame(frame);
            var members = copies.Select(BuildFrameBytes).ToArray();
            byte[] buffer = new byte[members.Sum(member => member.Length)];
            int offset = 0;
            foreach (var member in members) { member.CopyTo(buffer, offset); offset += member.Length; }
            phase = "AdapterLock"; long queued = Stopwatch.GetTimestamp();
            budget.Enter(_transmitGate, ct, checkAdmission); entered = true;
            lockMs = Stopwatch.GetElapsedTime(queued).TotalMilliseconds;
            ((ICanBudgetedBatchSubmitter)this).VerifySubmissionAvailable();
            ThrowIfReceivePumpFailed(); checkAdmission(); _ = budget.Remaining(ct);
            // A genuinely unresolved diagnostic transaction is not converted to
            // untracked success by switching modes. Normal submit-only traffic
            // registers no tickets and performs no session/echo polling.
            if (!_transmits.Quiescent)
                throw new InvalidOperationException("NI 仍有旧确认事务未退出，请完成旧事务或受控关闭；未提交本批");
            // The old diagnostic transaction really settled. Do not let its
            // strategy leak into later ordinary SDO writes after this mode's
            // first submission. Re-entering strict mode still drains untracked TX.
            _strictEnabled = false;
            admitted = true; progress(new(CanBatchProgressKind.NativeAdmission, copies.Length));
            publish(Evidence()); checkAdmission(); int remaining = budget.Remaining(ct);
            bool previouslyUntracked = _hasUntrackedSubmissions;
            attempted = true; nativeStart = Stopwatch.GetTimestamp(); state = "AcceptanceUnknown"; phase = "NativeWrite";
            _hasUntrackedSubmissions = true;
            try
            {
                int status = _io.WriteBatch(_txSession, buffer, remaining / 1000d);
                if (status == NiXnetApi.nxErrEventTimeout)
                { accepted = 0; _hasUntrackedSubmissions = previouslyUntracked; state = "NoneQueuedSdkTimeout"; }
                _io.Check(status); accepted = copies.Length; returned = true; state = "AcceptedBySdk";
            }
            catch { if (accepted != 0) _unknownSend = true; throw; }
            finally { exited = true; nativeEnd = Stopwatch.GetTimestamp(); GC.KeepAlive(buffer); }
            progress(new(CanBatchProgressKind.SubmissionCompleted, copies.Length));
            _ = budget.Remaining(ct); phase = "Completed";
        }
        catch (Exception ex)
        {
            error = ex;
            if (admitted && !attempted)
                try { progress(new(CanBatchProgressKind.NativeNotEntered, copies.Length)); }
                catch (Exception rollback) { error = new AggregateException("NI 提交与未进入SDK的计数回收均失败", ex, rollback); }
        }
        finally
        {
            try
            {
                publish(Evidence());
                if (error == null)
                    try { _ = budget.Remaining(ct); }
                    catch (Exception deadlineError) { error = deadlineError; }
            }
            catch (Exception diagnosticError)
            { error = error == null ? diagnosticError : new AggregateException("NI 提交与诊断均失败", error, diagnosticError); }
            finally { if (entered) Monitor.Exit(_transmitGate); }
        }
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
