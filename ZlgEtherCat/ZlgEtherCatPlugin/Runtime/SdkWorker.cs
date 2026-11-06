using System.Collections.Concurrent;
using System.Diagnostics;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Runtime;

public sealed record SdkThreadPriorityStatus(AcquisitionPriority? Requested, string? Actual, string? RestoreTarget, string? Error);

/// <summary>唯一原生调用线程。取消只终止等待；关闭排在真实调用退出之后。</summary>
public sealed class SdkWorker
{
    private interface IWork { void Run(SdkWorker owner); void Reject(Exception error); }
    private sealed class Work<T>(Func<T> action, int timeoutMs, CancellationToken token) : IWork
    {
        private readonly long _startedAt = Stopwatch.GetTimestamp();
        internal readonly TaskCompletionSource<T> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // 0: Pending, 1: Running (possibly still in native code), 2: Withdrawn.
        private int _admission;
        public void Run(SdkWorker owner)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds >= timeoutMs) throw new TimeoutException($"排队已超过 {timeoutMs} ms，未提交 SDK 调用");
                owner.ThrowIfUnavailable();
                owner._beforeAdmission?.Invoke();
                if (Interlocked.CompareExchange(ref _admission, 1, 0) != 0) return;
                token.ThrowIfCancellationRequested();
                if (Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds >= timeoutMs)
                    throw new TimeoutException($"提交前已超过 {timeoutMs} ms，未执行 SDK 调用");
                var value = action();
                token.ThrowIfCancellationRequested();
                if (Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds >= timeoutMs) throw new TimeoutException($"SDK 调用已超过 {timeoutMs} ms");
                Result.TrySetResult(value);
            }
            catch (Exception ex) { Result.TrySetException(ex); }
        }
        public bool Withdraw(Exception error)
        {
            var previous = Interlocked.CompareExchange(ref _admission, 2, 0);
            if (previous == 0) Result.TrySetException(error);
            return previous == 1;
        }
        public void Reject(Exception error) => Withdraw(error);
    }

    private readonly ConcurrentQueue<IWork> _queue = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly object _state = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _closing;
    private Action? _release;
    private string? _isolated;
    private readonly Action? _beforeAdmission;
    private readonly Thread _thread;
    private readonly Action<ThreadPriority> _setPriority;
    private readonly Func<ThreadPriority> _getPriority;
    private ThreadPriority? _restorePriority;
    private SdkThreadPriorityStatus _priorityStatus = new(null, nameof(ThreadPriority.Normal), null, null);
    public Action? Tick { get; set; }
    public Action<Exception>? BackgroundError { get; set; }
    public string? IsolatedReason => Volatile.Read(ref _isolated);
    public int WorkerStarts => 1;
    public SdkThreadPriorityStatus PriorityStatus => Volatile.Read(ref _priorityStatus);

    public SdkWorker(string name) : this(name, null) { }

    // 可控并发测试点：停在期限检查和原生提交之间；生产构造不提供回调。
    internal SdkWorker(string name, Action? beforeAdmission, Action<ThreadPriority>? setPriority = null, Func<ThreadPriority>? getPriority = null)
    {
        _beforeAdmission = beforeAdmission;
        _setPriority = setPriority ?? (priority => Thread.CurrentThread.Priority = priority);
        _getPriority = getPriority ?? (() => Thread.CurrentThread.Priority);
        _thread = new Thread(Run) { IsBackground = true, Name = "ZLG SDK " + name };
        if (ExecutionContext.IsFlowSuppressed()) _thread.Start();
        else { using (ExecutionContext.SuppressFlow()) _thread.Start(); }
    }

    internal void SetAcquisitionPriority(AcquisitionPriority requested)
    {
        VerifyWorkerThread();
        var desired = requested switch
        {
            AcquisitionPriority.Normal => ThreadPriority.Normal,
            AcquisitionPriority.AboveNormal => ThreadPriority.AboveNormal,
            AcquisitionPriority.Highest => ThreadPriority.Highest,
            _ => throw new ArgumentOutOfRangeException(nameof(requested), "采集线程优先级未定义")
        };
        ThreadPriority previous;
        try { previous = _getPriority(); }
        catch (Exception ex)
        {
            UpdatePriorityStatus(requested, ex);
            throw new InvalidOperationException("无法读取 SDK 线程当前优先级，采集未启动", ex);
        }
        var previousRestore = _restorePriority;
        _restorePriority ??= previous;
        try
        {
            ApplyPriority(desired);
            Volatile.Write(ref _priorityStatus, new(requested, desired.ToString(), _restorePriority?.ToString(), null));
        }
        catch (Exception ex)
        {
            Exception failure = ex;
            try { ApplyPriority(previous); _restorePriority = previousRestore; }
            catch (Exception rollback) { failure = new AggregateException("优先级设置失败且恢复原值未确认", ex, rollback); }
            UpdatePriorityStatus(requested, failure);
            throw new InvalidOperationException("SDK 采集线程优先级应用未确认，采集未启动；请查看 Priority 状态", failure);
        }
    }

    internal void RestoreAcquisitionPriority()
    {
        VerifyWorkerThread();
        if (_restorePriority is not { } previous) return;
        try
        {
            ApplyPriority(previous);
            _restorePriority = null;
            Volatile.Write(ref _priorityStatus, new(PriorityStatus.Requested, previous.ToString(), null, null));
        }
        catch (Exception ex)
        {
            UpdatePriorityStatus(PriorityStatus.Requested, ex);
            throw new InvalidOperationException("采集已停止，但 SDK 线程优先级恢复未确认；仍可重试停止或关闭通道", ex);
        }
    }

    private void ApplyPriority(ThreadPriority priority)
    {
        _setPriority(priority);
        if (_getPriority() != priority) throw new InvalidOperationException("操作系统回读的线程优先级与请求不一致");
    }

    private void UpdatePriorityStatus(AcquisitionPriority? requested, Exception? error)
    {
        string? actual = null;
        try { actual = _getPriority().ToString(); }
        catch (Exception readError) { error = error is null ? readError : new AggregateException(error, readError); }
        Volatile.Write(ref _priorityStatus, new(requested, actual, _restorePriority?.ToString(), error?.ToString()));
    }

    private void VerifyWorkerThread()
    {
        if (Thread.CurrentThread != _thread) throw new InvalidOperationException("只能在既有 SDK 线程内部修改采集优先级");
    }

    public async Task<T> InvokeAsync<T>(Func<T> action, int timeoutMs, CancellationToken token)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        token.ThrowIfCancellationRequested();
        var work = new Work<T>(action, timeoutMs, token);
        lock (_state) { ThrowIfUnavailable(); if (_queue.Count >= 1024) throw new InvalidOperationException("SDK 工作队列已满"); _queue.Enqueue(work); _wake.Set(); }
        try { return await work.Result.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), token).ConfigureAwait(false); }
        catch (Exception ex) when (ex is TimeoutException || ex is OperationCanceledException)
        {
            if (work.Withdraw(ex)) Isolate("调用已提交后超时或取消，结果未确认；必须关闭通道，禁止继续使用");
            _ = work.Result.Task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    public Task InvokeAsync(Action action, int timeoutMs, CancellationToken token) => InvokeAsync(() => { action(); return true; }, timeoutMs, token);
    public void Isolate(string reason) => Interlocked.CompareExchange(ref _isolated, reason, null);

    private void ThrowIfUnavailable()
    {
        if (_closing) throw new InvalidOperationException("通道正在关闭，禁止提交新操作");
        if (IsolatedReason != null) throw new InvalidOperationException(IsolatedReason);
    }

    public Task CloseAsync(Action release)
    {
        lock (_state)
        {
            if (!_closing) { _closing = true; _release = release; _wake.Set(); }
            return _closed.Task;
        }
    }

    private void Run()
    {
        try
        {
            while (true)
            {
                lock (_state) if (_closing) break;
                // Bound command bursts so pool configuration traffic cannot indefinitely starve input.
                int count = 0;
                while (count++ < 8 && _queue.TryDequeue(out var work)) work.Run(this);
                if (!_closing && IsolatedReason == null && Tick is { } tick)
                {
                    try { tick(); }
                    catch (Exception ex)
                    {
                        Isolate("后台 SDK 交换失败：" + ex.Message);
                        try { BackgroundError?.Invoke(ex); }
                        catch (Exception logError) { Interlocked.Exchange(ref _isolated, IsolatedReason + "；后台错误报告失败：" + logError.Message); }
                    }
                }
                _wake.WaitOne(Tick == null ? Timeout.Infinite : 1);
            }
            while (_queue.TryDequeue(out var work)) work.Reject(new InvalidOperationException("通道关闭，排队操作未执行"));
            // Priority failures must not prevent native handle cleanup or real thread exit.
            Exception? restoreError = null;
            try { RestoreAcquisitionPriority(); } catch (Exception ex) { restoreError = ex; }
            try { _release?.Invoke(); }
            catch (Exception ex) when (restoreError != null) { throw new AggregateException(restoreError, ex); }
            if (restoreError is null) _closed.TrySetResult();
            else _closed.TrySetException(restoreError);
        }
        catch (Exception ex) { Isolate("关闭或工作线程失败：" + ex.Message); _closed.TrySetException(ex); }
        finally { _wake.Dispose(); }
    }
}
