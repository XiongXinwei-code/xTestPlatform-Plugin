using System.Diagnostics;
using CAN.CANopen;
using CAN.Helpers;
using CAN.Models;
using PluginCommon;

namespace CAN.Runtime;

internal sealed record ProtectedCanFrame(CanMessage Frame, CanPayloadProtectionSetting Protection, string SenderKey, int SourceRow,
    CanPayloadProcessor? Processor = null, CanConnectionResource.CanCyclicConfiguration? Cyclic = null);

public sealed partial class CanConnectionResource
{
    internal async Task<CanMessage[]> WriteProtectedBatchAsync(ProtectedCanFrame[] frames, int timeoutMs, CancellationToken ct = default,
        Action? checkLifetime = null, Action<Task>? trackActual = null, bool isolateOnCancellation = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        if (frames == null || frames.Length is < 1 or > 128) throw new ArgumentException("CAN 多报文发送需要 1～128 条已启用报文");
        var started = Stopwatch.GetTimestamp();
        var snapshot = frames.Select(f => f == null ? throw new ArgumentException("CAN 报文行不能为空") :
            new ProtectedCanFrame(CanReceiveRouter.Copy(f.Frame), f.Protection?.Copy() ?? new(), f.SenderKey, f.SourceRow, f.Processor, f.Cyclic)).ToArray();
        var progress = new MultiWriteProgress();
        void Freeze(bool isolate = true)
        {
            if (progress.Freeze() && isolate) Isolate("CAN 多报文步骤停止等待，已准入的原生提交尚未返回；保留所有者直至实际退出");
        }
        using var operation = ExecutionScopedExecutor.OwnOperation(() => Freeze());
        using var cancellation = ct.Register(() => Freeze(isolateOnCancellation));
        var actual = Task.Run(() => WriteBatchCore(snapshot, started, timeoutMs, ct, progress, checkLifetime), CancellationToken.None);
        trackActual?.Invoke(actual);
        try
        {
            if (ForegroundWaitSignal is { } stop)
                await stop.WaitAsync(actual, TimeSpan.FromMilliseconds(timeoutMs), ct, "CAN 多报文发送").ConfigureAwait(false);
            else await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException("CAN 多报文提交返回时已超过配置总期限");
            return snapshot.Select(f => f.Frame).ToArray();
        }
        catch (Exception ex)
        {
            Freeze(ex is not OperationCanceledException || isolateOnCancellation);
            _ = actual.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            if (ex is CanCyclicChangedException) throw;
            string detail = $"CAN 多报文发送未完成：原列表第 {Volatile.Read(ref progress.Row)} 行；" +
                $"已尝试={Volatile.Read(ref progress.Attempted)}，驱动已返回成功={Volatile.Read(ref progress.Returned)}/{snapshot.Length}；" +
                $"原生调用尚未退出={Volatile.Read(ref progress.NativeActive) != 0}，批次任务已退出={actual.IsCompleted}；" +
                $"总期限={timeoutMs} ms。已尝试报文不撤回、不自动重试。{ex.Message}";
            if (ex is OperationCanceledException && ct.IsCancellationRequested) throw new OperationCanceledException(detail, ex, ct);
            if (ex is TimeoutException) throw new TimeoutException(detail, ex);
            throw new InvalidOperationException(detail, ex);
        }
    }

    private void WriteBatchCore(ProtectedCanFrame[] frames, long started, int timeoutMs, CancellationToken ct, MultiWriteProgress progress,
        Action? checkLifetime)
    {
        void Check()
        {
            checkLifetime?.Invoke();
            CanExecutionGuard.CheckSubmission(); ExecutionScopedExecutor.ThrowIfCurrentStepRetired();
            ct.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"CAN 多报文发送超过配置总期限 {timeoutMs} ms，未提交下一帧");
            lock (_gate) CheckLocked();
        }
        Check();
        var sources = new HashSet<(string, uint, CanFrameType, bool)>();
        foreach (var row in frames)
        {
            Volatile.Write(ref progress.Row, row.SourceRow);
            CanHelper.ValidateTransmitFrame(row.Frame);
            CanPayloadProtection.Validate(row.Protection, row.Frame.Data.Length);
            if (row.Processor != null && !row.Processor.Matches(row.Protection))
                throw new ArgumentException("周期报文的计数器与保护配置不一致，整批未发送");
            if (row.SourceRow <= 0 || row.Protection.RcEnabled && string.IsNullOrWhiteSpace(row.SenderKey))
                throw new ArgumentException("CAN 多报文行号或 RC 来源标识无效");
            if (!sources.Add((row.SenderKey, row.Frame.Id, row.Frame.FrameType, row.Frame.IsFd)))
                throw new ArgumentException("CAN 多报文行的计数来源重复，请保留独立的原列表位置");
            lock (_gate)
            {
                CheckLocked();
                if (row.Frame.IsFd && _openedProtocol == CanProtocolType.Classic)
                    throw new ArgumentException("连接以 Classic CAN 打开，列表包含 CAN FD 帧，整批未发送");
            }
        }
        var budget = new CanConfirmationBudget(started, timeoutMs, ct); // Queue deadline only; never waits for echo.
        using var protocol = CanopenConnectionAccess.For(this).EnterSupplementalSubmission(
            frames.Any(f => f.Frame.FrameType == CanFrameType.Standard && !f.Frame.IsFd && f.Frame.Id is >= 0x601 and <= 0x67F), budget, ct, Check);
        using var turn = _txGate.Enter(budget, ct, Check, frames[0].Frame.Id);
        Check();
        int additional = 0;
        foreach (var row in frames)
        {
            Volatile.Write(ref progress.Row, row.SourceRow);
            var key = (row.SenderKey, row.Frame.Id, row.Frame.FrameType, row.Frame.IsFd);
            var candidate = row.Processor ?? (row.Protection.RcEnabled && _payloadCounters.TryGetValue(key, out var old) && old.Matches(row.Protection)
                ? old : new CanPayloadProcessor(row.Protection));
            _ = candidate.Prepare(row.Frame.Data);
            // Preflight does not install/reset/delete an unsent row's counter.
            if (row.Processor == null && row.Protection.RcEnabled && !_payloadCounters.ContainsKey(key)) additional++;
        }
        if (_payloadCounters.Count + additional > 4096) throw new InvalidOperationException("本连接 RC 计数来源将超过 4096 项，整批未发送");
        Check();
        lock (_gate) { CheckLocked(); _activeIo++; }
        try
        {
            foreach (var row in frames)
            {
                Volatile.Write(ref progress.Row, row.SourceRow); Check();
                WriteUnderSubmissionGate(row.Frame, Check, processor: row.Processor,
                    protection: row.Processor == null ? row.Protection : null, senderKey: row.SenderKey,
                    attempting: () => progress.EnterAttempt(Check),
                    nativeReturned: () => Interlocked.Increment(ref progress.Returned), nativeExited: progress.ExitAttempt, cyclic: row.Cyclic);
            }
        }
        finally { lock (_gate) _activeIo--; }
    }

    internal sealed class MultiWriteProgress
    {
        private readonly object _admission = new();
        private bool _frozen;
        internal int Row, Attempted, Returned, NativeActive;
        // Freeze and admission share a short boundary. No lock is held across SDK I/O.
        // Admission wins: ownership stays until the attempt exits. Freeze wins: no
        // counter commit and no native call are allowed, even after a prior check.
        internal bool Freeze()
        {
            lock (_admission) { _frozen = true; return NativeActive != 0; }
        }
        internal void EnterAttempt(Action check)
        {
            lock (_admission)
            {
                if (_frozen) throw new OperationCanceledException("CAN 多报文批次已冻结，不再准入后续提交");
                check();
                Interlocked.Increment(ref Attempted); Volatile.Write(ref NativeActive, 1);
            }
        }
        internal void ExitAttempt() { lock (_admission) Volatile.Write(ref NativeActive, 0); }
    }
}
