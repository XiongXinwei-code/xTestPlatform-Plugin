using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using PluginCommon;

namespace OpcUa.Helpers;

/// <summary>外层等待可结束，消费门只由实际转换/导出工作者释放。</summary>
internal sealed class OpcUaConsumptionQueue(ConcurrentQueue<DataAcqRecord> buffer, SemaphoreSlim consumer)
{
    private readonly object _state = new();
    private readonly HashSet<Task> _actual = [];
    private readonly OwnerStopSignal _stop = new();
    private bool _frozen;
    private long _lastExportCommitSequence;
    private string? _lastError;
    public string? LastError => Volatile.Read(ref _lastError);
    public long LastExportCommitSequence => Interlocked.Read(ref _lastExportCommitSequence);
    // 这里只确认真实退出；结果由原等待者和 LastError 传播，不能把已报告的历史故障当作尚未退出。
    public Task Completion { get { lock (_state) return Task.WhenAll(_actual.Select(ObserveExitAsync).ToArray()); } }
    public int PendingCount { get { lock (_state) return _actual.Count; } }
    public void Freeze()
    {
        lock (_state)
        {
            _frozen = true;
            // 只通知前台结束等待；真实导出与取消回调仍归本队列，不触及其消费门。
            _stop.RequestStop();
        }
    }
    private bool IsFrozen { get { lock (_state) return _frozen; } }

    public Task<T> RunAsync<T>(int maxCount, Func<IReadOnlyList<DataAcqRecord>, Consumption, Task<T>> prepare,
        int timeoutMs, CancellationToken ct, Action<T>? publish = null)
    {
        if (maxCount == 0 || maxCount < -1) throw new ArgumentOutOfRangeException(nameof(maxCount));
        if (timeoutMs != Timeout.Infinite && timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        ct.ThrowIfCancellationRequested();
        var operation = new Consumption(timeoutMs, ct, () => IsFrozen);
        var prepared = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task actual;
        lock (_state)
        {
            if (_frozen) { operation.Dispose(); throw new InvalidOperationException("OPC UA 采集已收到停止请求，不能开始新的消费"); }
            actual = Task.Run(async () =>
            {
                bool entered = false;
                bool exportCommitted = false;
                Exception? failure = null;
                IReadOnlyList<DataAcqRecord> records = [];
                try
                {
                    await consumer.WaitAsync(operation.Token).ConfigureAwait(false);
                    entered = true;
                    operation.ThrowIfSubmissionStopped();
                    var snapshot = buffer.ToArray();
                    records = maxCount < 0 ? snapshot : snapshot.Take(maxCount).ToArray();
                    T result = await prepare(records, operation).ConfigureAwait(false);
                    // CSV 已实际提交时，即使等待者离开也提交 FIFO，避免下次重复导出。
                    // 未产生外部输出的放弃调用不消费数据。
                    if (operation.ExternalOutputCommitted) CommitExport();
                    else operation.ThrowIfActive();
                    prepared.TrySetResult(result);
                    if (await operation.Accepted.ConfigureAwait(false) && !operation.ExternalOutputCommitted) Dequeue(records.Count);
                }
                catch (Exception ex)
                {
                    // 外部写入成功与后续转换/发布成功不是同一事务，已经确认的 CSV 不得再次导出。
                    if (operation.ExternalOutputCommitted) CommitExport();
                    failure = ex;
                    prepared.TrySetException(ex);
                }
                finally
                {
                    // CancelAsync 的真实回调也是旧事务的一部分，不能在回调仍执行时放消费门。
                    try { await operation.SealCancellationAndGetCompletion().ConfigureAwait(false); }
                    catch (Exception cleanup)
                    {
                        failure = failure == null ? cleanup : new AggregateException("OPC UA 消费与原生取消回调均失败", failure, cleanup);
                        prepared.TrySetException(failure);
                    }
                    finally { operation.Dispose(); if (entered) consumer.Release(); }
                }
                if (failure != null)
                {
                    // finally 中的取消回调失败不能覆盖原始消费故障，也不能随任务移出集合而丢失。
                    Volatile.Write(ref _lastError, failure.ToString());
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }

                void CommitExport()
                {
                    if (exportCommitted) return;
                    Dequeue(records.Count); exportCommitted = true;
                    if (records.Count > 0) Interlocked.Exchange(ref _lastExportCommitSequence, records[^1].Sequence);
                }
            }, CancellationToken.None);
            _actual.Add(actual);
        }
        _ = prepared.Task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        _ = actual.ContinueWith(t =>
        {
            if (t.IsFaulted) _ = t.Exception;
            lock (_state) _actual.Remove(actual);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return WaitForPreparedAsync();

        async Task<T> WaitForPreparedAsync()
        {
            try
            {
                T result = await _stop.WaitAsync(prepared.Task, operation.Remaining, ct, "OPC UA 采集消费").ConfigureAwait(false);
                operation.ThrowIfActive();
                // 只有本次仍在等待的前台允许发布变量；后台导出永远不调用序列上下文。
                publish?.Invoke(result);
                operation.Accept();
                await _stop.WaitAsync(actual, operation.Remaining, ct, "OPC UA 采集消费收尾").ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                operation.Abandon();
                if (ex is TimeoutException)
                    throw new TimeoutException($"OPC UA 消费/转换/导出超过总期限 {timeoutMs} ms；实际事务已退出={actual.IsCompleted}；未退出时仍保留消费门与缓冲归属", ex);
                throw;
            }
        }

        void Dequeue(int count) { for (int i = 0; i < count; i++) buffer.TryDequeue(out _); }
    }

    private async Task ObserveExitAsync(Task actual)
    {
        try { await actual.ConfigureAwait(false); }
        catch (Exception ex) { Volatile.Write(ref _lastError, ex.ToString()); }
    }

    internal sealed class Consumption(int timeoutMs, CancellationToken caller, Func<bool> frozen) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly CancellationTokenSource _cancel = new();
        private readonly TaskCompletionSource<bool> _accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _state = new();
        private Task _callbacks = Task.CompletedTask;
        private bool _abandoned, _disposed, _sealed;
        public CancellationToken Token => _cancel.Token;
        public Task<bool> Accepted => _accepted.Task;
        public bool ExternalOutputCommitted { get; private set; }
        public Task SealCancellationAndGetCompletion()
        {
            lock (_state)
            {
                // 捕获与封闭必须是同一个状态转换；finally 取快照后不得再启动新的原生取消回调。
                _sealed = true;
                return _callbacks;
            }
        }
        public TimeSpan Remaining => timeoutMs == Timeout.Infinite ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromMilliseconds(Math.Max(0, timeoutMs - Stopwatch.GetElapsedTime(_started).TotalMilliseconds));
        public void ThrowIfActive()
        {
            caller.ThrowIfCancellationRequested();
            lock (_state) if (_abandoned) throw new OperationCanceledException("OPC UA 消费等待已结束，禁止发布晚到结果");
            if (timeoutMs != Timeout.Infinite && Stopwatch.GetElapsedTime(_started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"OPC UA 消费总期限 {timeoutMs} ms 已到，不能开始后续转换/导出/发布");
        }
        public void ThrowIfSubmissionStopped()
        {
            ThrowIfActive();
            if (frozen()) throw new InvalidOperationException("OPC UA 采集已停止，禁止开始后续导出");
        }
        public void MarkExternalOutputCommitted() => ExternalOutputCommitted = true;
        public void Accept() => _accepted.TrySetResult(true);
        public void Abandon()
        {
            lock (_state)
            {
                _abandoned = true;
                _accepted.TrySetResult(false);
                if (!_disposed && !_sealed && !_cancel.IsCancellationRequested) _callbacks = _cancel.CancelAsync();
            }
        }
        public void Dispose() { lock (_state) { if (_disposed) return; _sealed = true; _disposed = true; _cancel.Dispose(); } }
    }
}
