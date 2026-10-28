using PluginCommon;
using System.Diagnostics;
using xTestPlatform.Core.Engine;

namespace Ethernet.Runtime;

/// <summary>
/// A temporary socket is still an execution resource. Caller waits may end before real I/O;
/// only the pool's retirement worker releases its native handle after actual exit.
/// </summary>
internal sealed class TransientNetworkOperation<T> : IDisposable where T : class, IDisposable
{
    private sealed class OperationCancellation : IDisposable
    {
        private readonly object _sync = new();
        private readonly CancellationTokenRegistration _registration;
        private Task? _actualCancellation;
        internal readonly CancellationTokenSource Source = new();
        internal readonly TaskCompletionSource Requested = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal OperationCancellation(CancellationToken caller)
        {
            // The original caller callback only publishes intent. Native callbacks never
            // execute synchronously as part of the caller/deadline cancellation chain.
            _registration = caller.UnsafeRegister(static state =>
                { _ = ((OperationCancellation)state!).RequestCancellation(); }, this);
        }
        internal Task RequestCancellation()
        {
            Requested.TrySetResult();
            lock (_sync) return _actualCancellation ??= Task.Run(async () => await Source.CancelAsync().ConfigureAwait(false));
        }
        public void Dispose() { _registration.Dispose(); Source.Dispose(); }
    }

    private sealed class Handle(T native) : IDisposable
    {
        private readonly object _sync = new();
        private readonly PendingIoGate _io = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly TaskCompletionSource _stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<OperationCancellation> _tokens = [];
        private bool _closing;
        private Task? _cancelCallbacks;
        private bool _nativeReleased;
        private Exception? _cancellationFailure;
        private int _cancellationReported;
        internal Exception? CancellationFailure => Volatile.Read(ref _cancellationFailure);

        internal async Task<TResult> RunAsync<TResult>(Func<T, CancellationToken, Task<TResult>> operation,
            CancellationToken caller, Action ensureExecution)
        {
            OperationCancellation cancellation;
            lock (_sync)
            {
                Check();
                cancellation = new OperationCancellation(caller);
                // The token remains owned until real I/O AND cancellation callbacks exit.
                _tokens.Add(cancellation);
            }
            void Check()
            {
                caller.ThrowIfCancellationRequested();
                ensureExecution();
                caller.ThrowIfCancellationRequested();
                if (_closing || _stop.IsCancellationRequested)
                    throw new InvalidOperationException("临时网络操作所属连接正在退出，不能提交或发布迟到 I/O");
            }
            try
            {
                var pending = _io.RunAsync(async () =>
                {
                    // Check raw owner state, not only a linked cancellation callback which may be delayed.
                    lock (_sync) Check();
                    return await operation(native, cancellation.Source.Token).ConfigureAwait(false);
                }, Timeout.Infinite, "临时网络通信", cancellation.Source.Token);
                // A native cancellation callback may itself be blocked. The host stop fence
                // does not wait for that callback; the real task and handle remain owned.
                if (await Task.WhenAny(pending, _stopRequested.Task, cancellation.Requested.Task).ConfigureAwait(false) != pending)
                {
                    _ = pending.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    lock (_sync) Check();
                }
                var result = await pending.ConfigureAwait(false);
                lock (_sync) Check();
                return result;
            }
            catch (OperationCanceledException)
            {
                lock (_sync) Check();
                throw;
            }
        }

        public void Dispose()
        {
            // NamedConnectionPool always invokes this on its retained background retirement worker.
            lock (_sync)
            {
                _closing = true;
                _stopRequested.TrySetResult();
                _cancelCallbacks ??= Task.Run(async () =>
                {
                    Task[] callbacks;
                    lock (_sync) callbacks = _tokens.Select(source => source.RequestCancellation()).Append(_stop.CancelAsync()).ToArray();
                    await Task.WhenAll(callbacks).ConfigureAwait(false);
                });
            }
            _io.RequestClose(() =>
            {
                // The I/O gate proves actual I/O exit, and this wait proves ALL cancellation
                // callbacks have exited. A completed callback fault is a diagnostic, not
                // evidence that the native handle must be reserved forever.
                try { _cancelCallbacks.GetAwaiter().GetResult(); }
                catch (Exception ex) { Interlocked.CompareExchange(ref _cancellationFailure, ex, null); }
                if (CancellationFailure is { } cancellationFailure && Interlocked.Exchange(ref _cancellationReported, 1) == 0)
                {
                    var reporting = Task.Run(() => Trace.TraceError($"临时网络取消回调已退出但失败：{cancellationFailure}"));
                    _ = reporting.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                try
                {
                    if (!_nativeReleased) { native.Dispose(); _nativeReleased = true; }
                }
                catch (Exception releaseFailure) when (CancellationFailure is not null)
                {
                    throw new AggregateException("临时网络取消回调失败且原生资源释放未确认，保留隔离", CancellationFailure, releaseFailure);
                }
                lock (_sync)
                {
                    foreach (var source in _tokens) source.Dispose();
                    _tokens.Clear();
                    _stop.Dispose();
                }
            });
            // This is the actual close task, never a fixed soft wait reported as successful release.
            _io.CloseCompletion!.GetAwaiter().GetResult();
        }
    }

    private static readonly NamedConnectionPool<Handle> Legacy = new();
    private readonly NamedConnectionPool<Handle> _pool;
    private readonly string _name;
    private readonly Handle _handle;
    private TransientNetworkOperation(NamedConnectionPool<Handle> pool, string name, Handle handle)
        => (_pool, _name, _handle) = (pool, name, handle);

    internal static async Task<TransientNetworkOperation<T>> OpenAsync(IResourceRegistry resources,
        Func<T> factory, int localUdpPort, int openTimeoutMs, CancellationToken caller, Action ensureExecution)
    {
        caller.ThrowIfCancellationRequested();
        ensureExecution();
        caller.ThrowIfCancellationRequested();
        var pool = NamedConnectionPool<Handle>.For(resources, $"Ethernet.Transient.{typeof(T).FullName}", Legacy);
        ensureExecution();
        caller.ThrowIfCancellationRequested();
        string name = Guid.NewGuid().ToString("N");
        // Port zero has no shared physical binding. Active SD multicast listeners retain ReuseAddress;
        // this shared key coordinates only retired owners across execution/debug resource tables.
        string key = localUdpPort > 0 ? $"Ethernet:Transient:UDP4:{localUdpPort}" : $"Ethernet:Transient:{name}";
        void Check()
        {
            caller.ThrowIfCancellationRequested();
            ensureExecution();
            caller.ThrowIfCancellationRequested();
        }
        var handle = await pool.OpenAsync(name, _ => { Check(); return Task.FromResult(new Handle(factory())); }, openTimeoutMs,
            caller, Check, key, removeAfterRetirement: true).ConfigureAwait(false);
        try
        {
            Check();
            return new(pool, name, handle);
        }
        catch { pool.RequestCloseOwned(name, handle); throw; }
    }

    internal Task<TResult> RunAsync<TResult>(Func<T, CancellationToken, Task<TResult>> operation,
        CancellationToken caller, Action ensureExecution) => _handle.RunAsync(operation, caller, ensureExecution);

    internal Task RunAsync(Func<T, CancellationToken, Task> operation, CancellationToken caller, Action ensureExecution) =>
        RunAsync(async (native, token) => { await operation(native, token).ConfigureAwait(false); return true; }, caller, ensureExecution);

    public void Dispose() => _pool.RequestCloseOwned(_name, _handle);
    internal Exception? CancellationFailure => _handle.CancellationFailure;
}
