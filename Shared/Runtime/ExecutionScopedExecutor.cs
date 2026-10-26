using System.Collections.Concurrent;
using System.Diagnostics;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace PluginCommon;

/// <summary>
/// SDK execution retirement and the caller's cancellation are independent. The
/// outer observer may return while the original executor/owners keep their real
/// tasks alive. Existing executor deadlines, submission and publication fences
/// remain authoritative; this class never releases a resource or cancels native I/O.
/// </summary>
internal sealed class ExecutionScopedExecutor(string module, Func<IStepExecutor> factory) : IStepExecutor
{
    private static readonly ConcurrentDictionary<long, Task<ExecutionResult>> Pending = new();
    private static long _sequence;
    private static readonly AsyncLocal<StepLifetime?> Current = new();
    internal static int PendingExecutionCount => Pending.Count;
    internal static IStepExecutor Create(string module, Func<IStepExecutor> factory) => new ExecutionScopedExecutor(module, factory);
    /// <summary>
    /// Register only a new owner or this step's exact generation. The callback must
    /// freeze managed admission and request asynchronous cleanup, never wait for a
    /// driver. Successful steps disarm these callbacks; their token is not the lifetime
    /// of a published axis, connection or acquisition task.
    /// </summary>
    internal static void Own(Action requestStop) => Current.Value?.Own(requestStop);
    /// <summary>
    /// An admitted transaction disarms after its result/cancellation fence. This is
    /// not proof of native exit; the resource's real task still owns that boundary.
    /// </summary>
    internal static IDisposable OwnOperation(Action requestStop)
    {
        var lifetime = Current.Value;
        var operation = new OperationStop(requestStop, lifetime);
        lifetime?.Own(operation.RequestStop);
        return operation;
    }
    internal static int CurrentOwnerRegistrationCount => Current.Value?.OwnerCount ?? 0;
    internal static void ThrowIfCurrentStepRetired() => Current.Value?.ThrowIfRetired();

    private sealed class OperationStop(Action requestStop, StepLifetime? lifetime) : IDisposable
    {
        private int _state;
        internal void RequestStop() { if (Interlocked.CompareExchange(ref _state, 2, 0) == 0) requestStop(); }
        public void Dispose()
        {
            Interlocked.CompareExchange(ref _state, 1, 0);
            lifetime?.Forget(RequestStop);
        }
    }
    private string DisplayName => module switch
    {
        "Ethernet" => "以太网", "SerialPort" => "串口", "Http" => "HTTP",
        "NiDaq" => "NI-DAQ", "OpcUa" => "OPC UA", _ => module
    };

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        ExecutionEpoch? epoch = null;
        Task<ExecutionResult>? actual = null;
        var lifetime = new StepLifetime();
        bool consumed = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            epoch = ExecutionEpoch.Capture(context.Resources, module);
            // Enter synchronously so existing executors snapshot their settings as
            // before. AsyncLocal flows into their continuations, not the host caller.
            var previous = Current.Value;
            Current.Value = lifetime;
            try { using (epoch.Bind()) actual = factory().ExecuteAsync(context, cancellationToken); }
            finally { Current.Value = previous; }
            Track(actual);
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var notification = cancellationToken.UnsafeRegister(state => ((TaskCompletionSource)state!).TrySetResult(), cancelled);
            await Task.WhenAny(actual, epoch.StopSignal.StopRequested, cancelled.Task).ConfigureAwait(false);
            if (actual.IsCompletedSuccessfully && actual.Result.StepResult.Status == TestStatus.Error)
            {
                consumed = true;
                return await PreserveErrorAsync(actual.Result, lifetime).ConfigureAwait(false);
            }
            if (cancellationToken.IsCancellationRequested || epoch.StopSignal.IsStopRequested)
            {
                var cleanupError = await RetireFailureAsync(lifetime).ConfigureAwait(false);
                if (actual.IsCompletedSuccessfully && actual.Result.StepResult.Status == TestStatus.Error)
                { consumed = true; return MergeCleanupError(actual.Result, cleanupError); }
                if (cleanupError != null) throw cleanupError;
                cancellationToken.ThrowIfCancellationRequested();
                epoch.ThrowIfExpired();
            }
            consumed = true;
            var result = await actual.ConfigureAwait(false);
            if (result.StepResult.Status == TestStatus.Error)
                return await PreserveErrorAsync(result, lifetime).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            epoch.ThrowIfExpired();
            lifetime.Complete();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var cleanupError = await RetireFailureAsync(lifetime).ConfigureAwait(false);
            return Aborted(cleanupError);
        }
        catch (Exception ex)
        {
            var cleanupError = await RetireFailureAsync(lifetime).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                return Aborted(cleanupError);
            if (cleanupError != null) ex = new AggregateException(ex, cleanupError);
            string message = epoch?.StopSignal.IsStopRequested == true
                ? $"{DisplayName} 当前执行资源作用域已回收或被新代次取代，旧步骤已结束等待；真实 I/O 仍由原所有者收尾"
                : $"{DisplayName} 步骤执行失败：{ex.Message}";
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error,
                Error = ErrorInfo.FromException(ex, message) } };
        }
        finally
        {
            if (actual != null && !consumed)
            {
                var diagnostic = Task.Run(() => ObserveLateAsync(actual), CancellationToken.None);
                _ = diagnostic.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
    }

    private static async Task<ExecutionResult> PreserveErrorAsync(ExecutionResult result, StepLifetime lifetime) =>
        MergeCleanupError(result, await RetireFailureAsync(lifetime).ConfigureAwait(false));

    private static ExecutionResult MergeCleanupError(ExecutionResult result, Exception? cleanupError)
    {
        if (cleanupError != null)
        {
            var error = result.StepResult.Error ??= new ErrorInfo { Message = "步骤执行失败" };
            error.Detail = (error.Detail ?? "") + Environment.NewLine + "本次新资源请求退出也失败，原始 Error 仍保留：" + cleanupError;
        }
        return result;
    }

    private static async Task<Exception?> RetireFailureAsync(StepLifetime lifetime)
    {
        try { await lifetime.RetireAsync().ConfigureAwait(false); return null; }
        catch (Exception ex) { return ex; }
    }

    private ExecutionResult Aborted(Exception? cleanupError) => new() { StepResult = new StepResult
    {
        Status = TestStatus.Aborted,
        Error = cleanupError == null ? null : ErrorInfo.FromException(cleanupError,
            $"{DisplayName} 步骤已取消，本次新建资源请求退出失败；保留原所有者，请检查收尾详情")
    } };

    private sealed class StepLifetime
    {
        private readonly object _sync = new();
        private readonly HashSet<Action> _owners = [];
        private int _state; // 0 active, 1 successful/disarmed, 2 retired
        private Task? _retirement;
        internal void Own(Action requestStop)
        {
            ArgumentNullException.ThrowIfNull(requestStop);
            lock (_sync)
            {
                if (_state == 1) return; // A legitimate background owner has its own lifetime now.
                if (_state == 0) { _owners.Add(requestStop); return; }
            }
            // A late constructor must not submit I/O; keep its exact staged object
            // reachable through cleanup even though the foreground already returned.
            ObserveFreeze(ScheduleFreeze([requestStop]));
            ThrowIfRetired();
        }
        internal int OwnerCount { get { lock (_sync) return _owners.Count; } }
        internal void Forget(Action requestStop) { lock (_sync) _owners.Remove(requestStop); }
        internal void ThrowIfRetired()
        {
            lock (_sync)
                if (_state == 2) throw new InvalidOperationException("本次步骤已退出，禁止旧表达式或初始化继续登记、提交 I/O 或发布结果");
        }
        internal void Complete() { lock (_sync) { if (_state == 0) _state = 1; _owners.Clear(); } }
        internal Task RetireAsync()
        {
            lock (_sync)
            {
                if (_state == 1) return Task.CompletedTask;
                _state = 2;
                if (_retirement == null)
                {
                    var owners = _owners.ToArray(); _owners.Clear();
                    _retirement = ScheduleFreeze(owners);
                    ObserveFreeze(_retirement);
                }
                return _retirement;
            }
        }
        private static Task ScheduleFreeze(Action[] owners)
        {
            if (owners.Length == 0) return Task.CompletedTask;
            Task Run() => Task.Run(() =>
            {
                List<Exception>? errors = null;
                foreach (var requestStop in owners)
                    try { requestStop(); } catch (Exception ex) { (errors ??= []).Add(ex); }
                if (errors != null) throw new AggregateException("本步骤新资源请求退出失败，真实释放仍由原所有者跟踪", errors);
            }, CancellationToken.None);
            if (ExecutionContext.IsFlowSuppressed()) return Run();
            using (ExecutionContext.SuppressFlow()) return Run(); // Cleanup is not an old step's new submission.
        }
        private static void ObserveFreeze(Task task) => _ = task.ContinueWith(t =>
        {
            _ = t.Exception;
            // Diagnostics run in a separate worker and never on a SDK cleanup/caller thread.
            Trace.TraceError("步骤新资源请求退出失败；真实 I/O 与释放保持原所有者，需核对收尾详情");
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private async Task ObserveLateAsync(Task<ExecutionResult> actual)
    {
        try
        {
            var result = await actual.ConfigureAwait(false);
            if (result.StepResult.Status == TestStatus.Error)
                Trace.TraceError($"{DisplayName} 旧步骤前台结束后，后台执行返回 Error；原始 I/O 与释放结果请查看原资源所有者的收尾日志");
        }
        catch (Exception ex)
        {
            // Keep arbitrary payloads, URLs, secrets and external exception text out
            // of this generic trace. Detailed native failures stay with their owner.
            Trace.TraceError($"{DisplayName} 旧步骤后台执行异常：{ex.GetType().Name}；真实退出和释放仍由原资源所有者跟踪");
        }
    }

    private static void Track(Task<ExecutionResult> actual)
    {
        long key = Interlocked.Increment(ref _sequence);
        Pending.TryAdd(key, actual);
        _ = actual.ContinueWith(t =>
        {
            _ = t.Exception;
            Pending.TryRemove(key, out _);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
