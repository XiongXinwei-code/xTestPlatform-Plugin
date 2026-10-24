using System.Diagnostics;
using System.Runtime.ExceptionServices;
using CiA402.Models;
using CiA402.Transport;

namespace CiA402.Core;

/// <summary>轴的执行期所有者。关闭只启动一次，自动回收不等待硬件或日志线程。</summary>
public sealed class AxisSession : IDisposable, IAsyncDisposable
{
    public const int ShutdownTimeoutMs = 2000;
    private readonly object _sync = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationTokenRegistration _lifetimeCancellation;
    private readonly AxisDiagnosticLog _log;
    private readonly int _shutdownTimeoutMs;
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _shutdown;
    private Task? _closing;
    private Task? _release;
    private TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _controlled;
    private bool _closed;
    private bool _controlRunning;
    private int _activeOperations;
    private sealed record Termination(DateTimeOffset Time, string Source, string Operation, Exception? Error, string Transport)
    {
        public string Summary => $"首次终止={Time:O}，来源={Source}，触发操作=[{Operation}]，原始原因={Error?.Message ?? Source}";
        public string Detail => $"{Summary}{Environment.NewLine}{Transport}{Environment.NewLine}{(Error == null ? "" : AxisFailureContext.ExceptionDetail(Error))}";
    }
    private Termination? _termination;
    private readonly string _bindingContext;
    private readonly string _sessionId = Guid.NewGuid().ToString("N")[..8];
    public AxisController Controller { get; }
    public BindAxisSetting Configuration { get; }
    /// <summary>本轴传输调用退出且实例释放完成；不代表父 CAN 的原生句柄已关闭。</summary>
    public Task Released => _released.Task;
    public string DiagnosticContext
    {
        get
        {
            Termination? termination;
            string state;
            lock (_sync)
            {
                state = $"会话={_sessionId}; {_bindingContext}; CommunicationTimeoutMs={Configuration.CommunicationTimeoutMs}; ShutdownTimeoutMs={_shutdownTimeoutMs}; 已终止={_closed}; 活动操作={_activeOperations}; 控制中={_controlRunning}; 底层释放完成={Released.IsCompletedSuccessfully}";
                termination = _termination;
            }
            // 格式化异常链不占用生命周期锁，也不额外读取驱动。
            return $"{state}{Environment.NewLine}{termination?.Detail ?? "首次终止=无"}{Environment.NewLine}{(Controller.Transport is CoordinatedAxisTransport coordinated ? coordinated.DiagnosticContext : "")}";
        }
    }
    public static string Key(string axisName) => "CiA402.Axis:" + axisName;

    /// <summary>由显式解绑及执行资源表回收关闭；不订阅创建该资源的步骤令牌。</summary>
    public AxisSession(AxisController controller, BindAxisSetting configuration, Action<string, Exception?> log, string bindingContext)
        : this(controller, configuration, CancellationToken.None, log, bindingContext) { }

    /// <summary>兼容显式外部生命周期令牌；runToken 必须覆盖整个轴会话，不能传步骤调用令牌。</summary>
    public AxisSession(AxisController controller, BindAxisSetting configuration, CancellationToken runToken, Action<string, Exception?> log)
        : this(controller, configuration, runToken, log, $"绑定配置表达式：轴={configuration.AxisName}; 连接={configuration.ConnectionName}; 后端={configuration.Transport}; 节点={configuration.NodeId}; 从站={configuration.SlaveAddress}") { }

    public AxisSession(AxisController controller, BindAxisSetting configuration, CancellationToken runToken, Action<string, Exception?> log, string bindingContext)
    {
        Controller = controller;
        Configuration = configuration;
        _bindingContext = bindingContext;
        _log = new AxisDiagnosticLog(log);
        if (configuration.ShutdownTimeoutMs is < 1 or > 3600000)
            throw new ArgumentOutOfRangeException(nameof(configuration.ShutdownTimeoutMs));
        _shutdownTimeoutMs = configuration.ShutdownTimeoutMs;
        _idle.SetResult();
        if (controller.Transport is CoordinatedAxisTransport coordinated)
            coordinated.CleanupError = ex => Report("轴通信实例释放失败", ex);
        // 仅供明确持有整个会话生命周期的调用方；BindAxis 使用不带令牌的构造入口。
        _lifetimeCancellation = runToken.UnsafeRegister(static state =>
            _ = ((AxisSession)state!).RequestClose(source: "显式轴会话生命周期令牌取消"), this);
    }

    public Task ExecuteAsync(Func<AxisController, CancellationToken, Task> action, bool control, int pollMs, CancellationToken ct)
        => ExecuteAsync(action, control, pollMs, ct, control ? "未命名控制操作" : "未命名只读操作");

    public async Task ExecuteAsync(Func<AxisController, CancellationToken, Task> action, bool control, int pollMs, CancellationToken ct, string operation)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_closed) throw new InvalidOperationException($"轴会话已终止，请先解除绑定后重新绑定；会话={_sessionId}；{_termination?.Summary}", _termination?.Error);
            if (control && _controlRunning) throw new InvalidOperationException("同一轴已有控制步骤执行中，不允许并发控制");
            if (_activeOperations != 0 && !Controller.Transport.SupportsConcurrentMonitoring)
                throw new InvalidOperationException("当前通信后端未声明独立监控能力，不允许同轴并发访问");
            if (_activeOperations++ == 0) _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (control) _controlRunning = true;
            _controlled |= control;
        }
        Exception? failure = null;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            if (control) Controller.PollIntervalMs = pollMs;
            await action(Controller, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            lock (_sync)
            {
                if (failure != null && control)
                    Terminate(failure is OperationCanceledException
                        ? $"控制步骤取消（步骤令牌取消={ct.IsCancellationRequested}，会话令牌取消={_lifetime.IsCancellationRequested}）"
                        : "控制步骤失败", operation, failure);
                if (control) _controlRunning = false;
                if (--_activeOperations == 0) _idle.TrySetResult();
            }
        }
        if (failure != null)
        {
            // 返回原始错误/取消，不用另一个通信超时拖延步骤结束。
            if (control) Dispose();
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public Task CloseAsync() => CloseAsync(CancellationToken.None);

    public Task CloseAsync(CancellationToken ct) => CloseAsync(ct, "显式关闭 CloseAsync");

    public async Task CloseAsync(CancellationToken ct, string operation)
    {
        var close = RequestClose(ct, "显式关闭", operation);
        var registration = ct.UnsafeRegister(static state => ((AxisSession)state!).CancelShutdown(), this);
        try { await close.WaitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // WaitAsync 的取消回调可能先恢复此方法；不能在取消清理之前撤销自己的注册。
            CancelShutdown();
            throw;
        }
        finally { registration.Unregister(); }
    }

    private Task RequestClose(CancellationToken ct = default, string source = "资源回收 Dispose（宿主或调用方）", string operation = "")
    {
        lock (_sync)
        {
            if (_closing != null) return _closing;
            Terminate(source, operation, null);
            _shutdown = new CancellationTokenSource(_shutdownTimeoutMs);
            if (ct.IsCancellationRequested) _shutdown.Cancel();
            _closing = Task.Run(() => CloseCoreAsync(_shutdown.Token));
            Observe(_closing);
            return _closing;
        }
    }

    // 调用者持有 _sync；先保存原因再开放其他操作，清理失败不能覆盖首故障。
    private void Terminate(string source, string operation, Exception? error)
    {
        _closed = true;
        if (_termination != null) return;
        var transport = Controller.Transport is CoordinatedAxisTransport coordinated ? coordinated.DiagnosticContext : "";
        _termination = new(DateTimeOffset.Now, source, operation, error, transport);
        Report($"轴会话终止：会话={_sessionId}; {_bindingContext}; {_termination.Summary}; {transport}", error);
    }

    private void CancelShutdown()
    {
        lock (_sync)
            if (_shutdown is { } shutdown) Observe(shutdown.CancelAsync());
    }

    private async Task CloseCoreAsync(CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        Report("开始清理轴会话", null);
        try
        {
            var cancellation = _lifetime.CancelAsync();
            Observe(cancellation);
            await cancellation.WaitAsync(ct).ConfigureAwait(false);
            Task idle;
            lock (_sync) idle = _idle.Task;
            await idle.WaitAsync(ct).ConfigureAwait(false);
            Report($"活动操作已退出（{clock.ElapsedMilliseconds} ms），开始停止运动与释放使能", null);
            if (_controlled)
            {
                var cleanup = Controller.CleanupAsync(Report, ct);
                Observe(cleanup);
                await cleanup.WaitAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            Report($"轴清理达到截止时间或被取消（{clock.ElapsedMilliseconds} ms），未确认驱动已停止", ex);
            throw new TimeoutException($"轴清理未在 {_shutdownTimeoutMs} ms 配置上限或调用方期限内完成，未确认驱动已停止", ex);
        }
        catch (Exception ex) { Report($"轴清理失败（{clock.ElapsedMilliseconds} ms），未确认驱动已停止", ex); throw; }
        finally
        {
            var release = EnsureRelease();
            try
            {
                if (!ct.IsCancellationRequested) await release.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) { throw new TimeoutException("轴底层释放超时，将在活动调用退出后完成释放", ex); }
            finally
            {
                // 禁止截止时间后继续提交控制字；实际未结束的 I/O 由传输层保留占用。
                CancelShutdown();
                Report($"轴清理等待结束（{clock.ElapsedMilliseconds} ms），底层释放完成={Released.IsCompletedSuccessfully}", null);
            }
        }
    }

    private Task EnsureRelease()
    {
        lock (_sync)
        {
            if (_release != null) return _release;
            _release = Task.Run(ReleaseCoreAsync);
            Observe(_release);
            return _release;
        }
    }

    private async Task ReleaseCoreAsync()
    {
        try
        {
            Task idle;
            lock (_sync) idle = _idle.Task;
            await idle.ConfigureAwait(false);
            if (Controller.Transport is IAsyncDisposable asyncTransport)
                await asyncTransport.DisposeAsync().ConfigureAwait(false);
            else Controller.Transport.Dispose();
            _released.TrySetResult();
        }
        catch (Exception ex)
        {
            Report("轴底层释放失败", ex);
            _released.TrySetException(ex);
            _ = _released.Task.Exception;
            throw;
        }
        finally
        {
            _lifetimeCancellation.Unregister();
        }
    }

    private void Report(string message, Exception? ex)
    {
        _log.Report(message, ex);
    }

    private static void Observe(Task task) => _ = task.ContinueWith(t => { _ = t.Exception; },
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    public void Dispose() { _ = RequestClose(); }
    public ValueTask DisposeAsync() => new(CloseAsync());
}
