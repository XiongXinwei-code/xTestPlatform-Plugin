using TaskAction = NationalInstruments.DAQmx.TaskAction;
using System.Diagnostics;
using PluginCommon;
using NiDaq.Models;
using DaqTask = NationalInstruments.DAQmx.Task;
using xTestPlatform.Core.Engine;
namespace NiDaq.Helpers;

/// <summary>执行上下文拥有复合任务；Abort 解除阻塞，原始 I/O 退出后才能释放。</summary>
public sealed class DaqTaskResource : IDisposable
{
    private readonly PendingIoGate _io = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _state = new();
    private readonly List<DaqTask> _tasks = new();
    private readonly Action<Exception>? _report;
    private readonly Action? _abortOverride, _releaseOverride;
    private readonly HashSet<DaqTask> _released = new();
    private readonly Dictionary<DaqTask, Exception> _disposeFailures = new(ReferenceEqualityComparer.Instance);
    private Task? _abort;
    private OperationBudget? _activeBudget;
    private AnalogOutputSession? _analogOutput;
    private long _cleanupStartedAt;
    private bool _cleaningOutput;
    private bool _configured, _closing;
    private bool _releasing, _releaseCompleted;
    private IResourceRegistry? _transientRegistry;
    private string? _transientKey;
    internal IReadOnlyList<string> PhysicalResourceKeys { get; }
    public bool IsClosed => _io.IsClosed;
    internal Exception? CloseWarning { get; private set; }
    internal bool IsClosing { get { lock (_state) return _closing; } }
    internal Action? ExecutionCheck { get; set; }
    internal void RegisterTransient(IResourceRegistry registry, Action ensureCurrentExecution)
    {
        ExecutionScopedExecutor.Own(Dispose);
        ensureCurrentExecution();
        _transientRegistry = registry;
        _transientKey = "NiDaq.Transient:" + Guid.NewGuid().ToString("N");
        if (!registry.TryAdd(_transientKey, this, ResourceLifetime.Execution))
            throw new InvalidOperationException("DAQ 临时任务登记失败，未提交原生操作");
        try { ensureCurrentExecution(); }
        catch { Dispose(); throw; }
    }
    private sealed record OperationBudget(long StartedAt, int TimeoutMs, string Description, CancellationToken CallerToken);
    public DaqTask Primary => _tasks[0];
    public IReadOnlyList<DaqTask> Counters => _analogOutput == null ? _tasks.Skip(1).ToArray() : [];
    public string[] CounterNames { get; set; } = [];
    public bool IsSync => _analogOutput == null && _tasks.Count > 1;
    public bool IsAnalogOutput => _analogOutput != null;
    public bool Started { get; private set; }
    public EncoderUnit Unit { get; set; } = EncoderUnit.Pulses;
    public double DistancePerPulse { get; set; } = 1;
    public DaqTaskResource(Action<Exception>? report = null, IEnumerable<string>? physicalChannels = null)
    { _report = report; PhysicalResourceKeys = physicalChannels == null ? [] : DaqPhysicalResources.Keys(physicalChannels); }
    internal DaqTaskResource(Action abort, Action release, IEnumerable<string>? physicalChannels = null)
    { _abortOverride = abort; _releaseOverride = release; PhysicalResourceKeys = physicalChannels == null ? [] : DaqPhysicalResources.Keys(physicalChannels); }
    public DaqTask AddTask()
    {
        EnsureOperationActive();
        var task = new DaqTask();
        // 构造返回的句柄必须先归属 owner，再检查期间发生的停止/期限。
        lock (_state) _tasks.Add(task);
        EnsureOperationActive();
        return task;
    }
    public Task ConfigureAsync(Action configure, CancellationToken ct, int timeoutMs = 10000)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var budget = new OperationBudget(Stopwatch.GetTimestamp(), timeoutMs, "DAQ 配置及设备路由验证", ct);
        lock (_state)
        {
            if (_closing) throw new InvalidOperationException("DAQ 配置已被停止请求冻结");
            return _io.RunAsync(async () =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
                lock (_state) _activeBudget = budget;
                try
                {
                foreach (var key in PhysicalResourceKeys)
                {
                    EnsureOperationActive();
                    int remaining = (int)Math.Ceiling(timeoutMs - Stopwatch.GetElapsedTime(budget.StartedAt).TotalMilliseconds);
                    if (remaining <= 0) throw new TimeoutException("DAQ 配置总期限已到，不能继续等待旧资源收尾");
                    await RetiringResources.WaitForPreviousAsync(key, remaining, linked.Token).ConfigureAwait(false);
                }
                EnsureOperationActive();
                configure();
                foreach (var task in _tasks) { EnsureOperationActive(); task.Control(TaskAction.Verify); }
                EnsureOperationActive();
                lock (_state) { if (_closing) throw new InvalidOperationException("DAQ 配置已终止"); _configured = true; }
                return true;
                }
                finally { lock (_state) _activeBudget = null; }
            }, timeoutMs, budget.Description, ct);
        }
    }
    public void EnsureReady() { ExecutionCheck?.Invoke(); lock (_state) if (_closing || !_configured) throw new InvalidOperationException("DAQ 任务尚未就绪或已请求停止"); }
    public Task<T> RunAsync<T>(Func<T> operation, int timeoutMs, string description, CancellationToken ct)
    {
        var budget = new OperationBudget(Stopwatch.GetTimestamp(), timeoutMs, description, ct);
        lock (_state)
        {
            if (_closing) throw new InvalidOperationException("DAQ 任务正在退出，不能提交新操作");
            return _io.RunBlockingAsync(() => ExecuteWithinBudget(operation, budget), timeoutMs, description, ct);
        }
    }
    private T ExecuteWithinBudget<T>(Func<T> operation, OperationBudget budget)
    {
        lock (_state) _activeBudget = budget;
        try
        {
            EnsureOperationActive();
            T result = operation();
            EnsureOperationActive();
            return result;
        }
        finally { lock (_state) _activeBudget = null; }
    }
    /// <summary>复合动作每次提交 SDK 调用前检查；等待者退出不等于原生动作已取消。</summary>
    internal void EnsureOperationActive()
    {
        ExecutionCheck?.Invoke(); // 不在 owner 锁内访问 SDK 资源表，避免与回收锁反转。
        lock (_state)
        {
            var budget = _activeBudget ?? throw new InvalidOperationException("DAQ 原生动作必须在 owner 的活动事务中提交");
            budget.CallerToken.ThrowIfCancellationRequested();
            if (_closing) throw new InvalidOperationException("DAQ 任务已收到停止请求，禁止继续提交后续原生动作");
            if (budget.TimeoutMs != Timeout.Infinite && Stopwatch.GetElapsedTime(budget.StartedAt).TotalMilliseconds >= budget.TimeoutMs)
                throw new TimeoutException($"{budget.Description}：复合事务总期限已到，禁止提交后续原生动作");
        }
    }
    internal int RemainingNativeTimeoutMs()
    {
        lock (_state)
        {
            if (_cleaningOutput)
            {
                EnsureOutputCleanupActive();
                return Math.Max(1, (int)Math.Ceiling(_analogOutput!.CleanupTimeoutMs - Stopwatch.GetElapsedTime(_cleanupStartedAt).TotalMilliseconds));
            }
        }
        // 普通提交先在 owner 锁外核对执行表；专用清理不经过已退休 epoch。
        EnsureOperationActive();
        lock (_state)
        {
            var budget = _activeBudget!;
            return budget.TimeoutMs == Timeout.Infinite ? Timeout.Infinite
                : Math.Max(1, (int)Math.Ceiling(budget.TimeoutMs - Stopwatch.GetElapsedTime(budget.StartedAt).TotalMilliseconds));
        }
    }
    internal void ConfigureAnalogOutput(AoOutputPlan plan, Action<double[]> write, Action start, Action stop, Action? prepareWrite = null)
    {
        EnsureOperationActive();
        lock (_state)
        {
            if (_closing) throw new InvalidOperationException("DAQ 任务已收到停止请求，不能建立 AO 输出状态");
            if (_analogOutput != null) throw new InvalidOperationException("AO 配置已存在");
            _analogOutput = new AnalogOutputSession(plan, write, start, stop, prepareWrite ?? (() => { }));
        }
    }
    public Task WriteAnalogAsync(double[] values, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length is 0 or > 256) throw new ArgumentException("AO 输出向量必须包含 1~256 个值", nameof(values));
        // 在排队前取得私有副本，调用方后续改数组不能改变将要发送的目标。
        var copy = (double[])values.Clone();
        return RunAsync(() =>
        {
            EnsureReady();
            (_analogOutput ?? throw new InvalidOperationException("任务不是 AO 单点输出任务")).Write(copy, EnsureOperationActive);
            return true;
        }, timeoutMs, "AO 单点电压输出", ct);
    }
    public Task StartAsync(CancellationToken ct, int timeoutMs = 10000) => RunAsync(() =>
    {
        EnsureReady();
        if (Started) throw new InvalidOperationException("DAQ 任务已启动");
        if (_analogOutput != null) { _analogOutput.Start(EnsureOperationActive); Started = true; return true; }
        // CI 先等待 AI SampleClock，AI 最后启动，避免丢失最初公共节拍。
        foreach (var counter in Counters) { EnsureOperationActive(); counter.Start(); }
        EnsureOperationActive(); Primary.Start();
        EnsureOperationActive(); Started = true; return true;
    }, timeoutMs, "DAQ 复合任务启动", ct);
    public void RequireStarted() { if (!Started) throw new InvalidOperationException("DAQ 任务尚未启动，请先执行 TaskStart"); }
    private void BeginClose()
    {
        // 先唤醒前台观察者；驱动 Abort、真实 I/O 与释放仍由后面的原任务拥有。
        _io.RequestStop();
        lock (_state)
        {
            if (_releaseCompleted || _io.IsClosed) return;
            if (!_closing || _io.CloseCompletion is { IsFaulted: true } or { IsCanceled: true })
                _cleanupStartedAt = Stopwatch.GetTimestamp();
            _closing = true;
            // AO 必须等待实际 writer 退出，然后按显式策略收尾；不能并发 Abort 改变输出语义。
            if (_analogOutput != null) { _abort = Task.CompletedTask; return; }
            if (_abort is null || (!_releasing && (_abort.IsFaulted || _abort.IsCanceled)))
            {
                var tasks = _configured ? _tasks.Where(task => !_released.Contains(task) && !_disposeFailures.ContainsKey(task)).ToArray() : [];
                _abort = Task.Run(() =>
                {
                    if (_abortOverride != null) { _abortOverride(); return; }
                    var errors = new List<Exception>();
                    foreach (var task in tasks) try { task.Control(TaskAction.Abort); } catch (Exception ex) { errors.Add(ex); }
                    if (errors.Count > 0) throw new AggregateException("DAQ Abort 未全部确认，保留句柄以便重试", errors);
                });
                _ = _abort.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }
    private void Release()
    {
        lock (_state) _releasing = true;
        try
        {
            ReleaseCore();
            lock (_state) _releaseCompleted = true;
        }
        finally { lock (_state) _releasing = false; }
    }
    private void ReleaseCore()
    {
        // 只在 gate 的后台收尾线程等待 Abort，不阻塞 UI/平台资源回收线程。
        Exception? abortError = null;
        try { _abort!.GetAwaiter().GetResult(); }
        catch (Exception ex) { abortError = ex; }
        if (_analogOutput != null)
        {
            lock (_state) _cleaningOutput = true;
            try { _analogOutput.Close(EnsureOutputCleanupActive); }
            finally { lock (_state) _cleaningOutput = false; }
        }
        if (_analogOutput != null) EnsureOutputCleanupActive();
        var errors = new List<Exception>();
        if (_releaseOverride != null)
        {
            try { _releaseOverride(); } catch (Exception ex) { errors.Add(ex); }
        }
        else
        {
            foreach (var task in _tasks.Where(task => !_released.Contains(task)))
            {
                if (_disposeFailures.TryGetValue(task, out var priorFailure)) { errors.Add(priorFailure); continue; }
                bool disposeEntered = false;
                try
                {
                    if (_analogOutput != null) EnsureOutputCleanupActive();
                    disposeEntered = true;
                    task.Dispose();
                    _released.Add(task);
                }
                catch (Exception ex)
                {
                    if (disposeEntered) _disposeFailures[task] = ex;
                    errors.Add(ex);
                }
            }
        }
        if (errors.Count > 0)
        {
            if (errors.Count == 1 && abortError == null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (abortError != null) errors.Insert(0, abortError);
            throw new AggregateException("DAQ 释放未全部完成，保留原实例；实际 Dispose 失败的实例状态未知，不以再次调用的空操作确认释放，其它实例可继续收尾", errors);
        }
        CloseWarning = abortError;
        Started = false;
    }
    private void EnsureOutputCleanupActive()
    {
        lock (_state)
            if (Stopwatch.GetElapsedTime(_cleanupStartedAt).TotalMilliseconds >= _analogOutput!.CleanupTimeoutMs)
                throw new TimeoutException($"AO 输出收尾超过 {_analogOutput.CleanupTimeoutMs} ms，未继续提交后续写入/停止，保留资源隔离；实际 I/O 退出后可显式 TaskStop 重试");
    }
    public Task CloseAsync(int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        BeginClose(); TrackRetirement();
        var closing = _io.CloseAsync(Release, timeoutMs, ct); ObserveRelease(); _lifetime.Cancel(); return closing;
    }
    public void Dispose() { BeginClose(); TrackRetirement(); _io.RequestClose(Release, _report); ObserveRelease(); _lifetime.Cancel(); }
    private void TrackRetirement()
    {
        if (!_io.IsClosed)
            foreach (var key in PhysicalResourceKeys)
                RetiringResources.Track(key, this, CloseAsync, () => _io.IsClosed, () => _io.IsolatedReason ?? "DAQ 正在关闭");
    }
    private void ObserveRelease()
    {
        if (_io.CloseCompletion is { } actual)
            _ = actual.ContinueWith(_ =>
            {
                foreach (var key in PhysicalResourceKeys) RetiringResources.ForgetReleased(key, this);
                if (_transientRegistry != null && _transientKey != null &&
                    _transientRegistry.TryGet<DaqTaskResource>(_transientKey, out var registered) && ReferenceEquals(registered, this))
                    _transientRegistry.Remove(_transientKey, dispose: false);
            }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
