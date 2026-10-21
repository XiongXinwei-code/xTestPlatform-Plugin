using System.Runtime.ExceptionServices;
using CiA402.Models;

namespace CiA402.Core;

public sealed class AxisSession : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationTokenRegistration _runCancellation;
    private readonly Action<string, Exception?> _log;
    private readonly int _cleanupWaitMs;
    private Task? _closing;
    private bool _controlled;
    private bool _closed;
    private bool _running;
    private bool _cleanupAfterOperation;
    private int _cleaned;
    public AxisController Controller { get; }
    public BindAxisSetting Configuration { get; }
    public static string Key(string axisName) => "CiA402.Axis:" + axisName;

    public AxisSession(AxisController controller, BindAxisSetting configuration, CancellationToken runToken, Action<string, Exception?> log)
    {
        Controller = controller;
        Configuration = configuration;
        _log = log;
        _cleanupWaitMs = configuration.CommunicationTimeoutMs * 2;
        _runCancellation = runToken.Register(() => { _ = CloseAsync(); });
    }

    public async Task ExecuteAsync(Func<AxisController, CancellationToken, Task> action, bool control, int pollMs, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false)) throw new InvalidOperationException("同一轴已有步骤执行中，不允许并发访问");
        Exception? failure = null;
        try
        {
            lock (_lifetime) _running = true;
            if (_closed) throw new InvalidOperationException("轴会话已终止，请先解除绑定后重新绑定");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            _controlled |= control;
            Controller.PollIntervalMs = pollMs;
            await action(Controller, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            bool finish;
            lock (_lifetime) { _running = false; finish = _cleanupAfterOperation; }
            _gate.Release();
            if (finish) await FinishCleanupAsync().ConfigureAwait(false);
        }
        if (failure != null)
        {
            if (control || ct.IsCancellationRequested) await CloseAsync().ConfigureAwait(false);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public Task CloseAsync()
    {
        lock (_lifetime)
        {
            if (_closing != null) return _closing;
            _closed = true;
            _lifetime.Cancel();
            return _closing = Task.Run(CloseCoreAsync);
        }
    }

    private async Task CloseCoreAsync()
    {
        try
        {
            if (!await _gate.WaitAsync(_cleanupWaitMs).ConfigureAwait(false))
            {
                bool finish;
                lock (_lifetime)
                {
                    _cleanupAfterOperation = _running;
                    finish = !_running;
                }
                _log($"轴会话清理等待超过 {_cleanupWaitMs} ms，将在活动操作退出后完成一次释放", null);
                if (finish) await FinishCleanupAsync().ConfigureAwait(false);
                return;
            }
            try
            {
                await FinishCleanupAsync().ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }
        catch (Exception ex) { _log("轴会话清理失败", ex); }
    }

    private async Task FinishCleanupAsync()
    {
        if (Interlocked.Exchange(ref _cleaned, 1) != 0) return;
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
