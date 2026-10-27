namespace PluginCommon;

/// <summary>Per-sender SDK write entry through real return; not bus confirmation or device state.</summary>
internal sealed class NativeWriteLifetime
{
    private readonly object _sync = new();
    private bool _frozen;
    private int _active;
    private TaskCompletionSource _idle = Completed();
    internal bool HasPending { get { lock (_sync) return _active != 0; } }
    internal Task Completion { get { lock (_sync) return _idle.Task; } }
    internal void Freeze() { lock (_sync) _frozen = true; }
    internal IDisposable Enter()
    {
        lock (_sync)
        {
            if (_frozen) throw new InvalidOperationException("周期发送任务已请求停止，禁止迟到的原生写入");
            if (_active++ == 0) _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new Ticket(this);
        }
    }
    private void Exit()
    {
        TaskCompletionSource? idle = null;
        lock (_sync) if (--_active == 0) idle = _idle;
        idle?.TrySetResult();
    }
    private static TaskCompletionSource Completed()
    {
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        idle.SetResult();
        return idle;
    }
    private sealed class Ticket(NativeWriteLifetime owner) : IDisposable
    {
        private NativeWriteLifetime? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Exit();
    }
}
