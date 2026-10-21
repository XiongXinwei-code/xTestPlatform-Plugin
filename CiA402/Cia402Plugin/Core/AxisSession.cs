using System.Runtime.ExceptionServices;
using CiA402.Models;
using CiA402.Transport;

namespace CiA402.Core;

public sealed class AxisSession : IDisposable
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationTokenRegistration _runCancellation;
    private readonly Action<string, Exception?> _log;
    private readonly int _cleanupWaitMs;
    private Task? _closing;
    private Task? _cleanup;
    private TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _controlled;
    private bool _closed;
    private bool _controlRunning;
    private int _activeOperations;
    public AxisController Controller { get; }
    public BindAxisSetting Configuration { get; }
    public static string Key(string axisName) => "CiA402.Axis:" + axisName;

    public AxisSession(AxisController controller, BindAxisSetting configuration, CancellationToken runToken, Action<string, Exception?> log)
    {
        Controller = controller;
        Configuration = configuration;
        _log = log;
        _cleanupWaitMs = configuration.CommunicationTimeoutMs * 2;
        _idle.SetResult();
        if (controller.Transport is CoordinatedAxisTransport coordinated)
            coordinated.CleanupError = ex => _log("轴通信实例释放失败", ex);
        _runCancellation = runToken.Register(() => { _ = CloseAsync(); });
    }

    public async Task ExecuteAsync(Func<AxisController, CancellationToken, Task> action, bool control, int pollMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_closed) throw new InvalidOperationException("轴会话已终止，请先解除绑定后重新绑定");
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
            bool finish;
            lock (_sync)
            {
                if (failure != null && control) _closed = true;
                if (control) _controlRunning = false;
                if (--_activeOperations == 0) _idle.TrySetResult();
                finish = _closed && _activeOperations == 0;
            }
            if (finish) await EnsureCleanupAsync().ConfigureAwait(false);
        }
        if (failure != null)
        {
            if (control) await CloseAsync().ConfigureAwait(false);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public Task CloseAsync()
    {
        lock (_sync)
        {
            if (_closing != null) return _closing;
            _closed = true;
            return _closing = Task.Run(CloseCoreAsync);
        }
    }

    private async Task CloseCoreAsync()
    {
        try
        {
            _lifetime.Cancel();
            Task idle;
            lock (_sync) idle = _idle.Task;
            try { await idle.WaitAsync(TimeSpan.FromMilliseconds(_cleanupWaitMs)).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                _log($"轴会话清理等待超过 {_cleanupWaitMs} ms，将在活动操作退出后完成一次释放", null);
                return;
            }
            await EnsureCleanupAsync().ConfigureAwait(false);
        }
        catch (Exception ex) { _log("轴会话清理失败", ex); }
    }

    private Task EnsureCleanupAsync()
    {
        lock (_sync) return _cleanup ??= Task.Run(FinishCleanupAsync);
    }

    private async Task FinishCleanupAsync()
    {
        try
        {
            if (_controlled) await Controller.CleanupAsync(_log).ConfigureAwait(false);
        }
        catch (Exception ex) { _log("轴会话清理失败", ex); }
        finally
        {
            try { Controller.Transport.Dispose(); }
            catch (Exception ex) { _log("轴通信实例释放失败", ex); }
        }
    }

    public void Dispose()
    {
        try { CloseAsync().GetAwaiter().GetResult(); }
        catch (Exception ex) { _log("轴会话释放失败", ex); }
        _runCancellation.Dispose();
    }
}
