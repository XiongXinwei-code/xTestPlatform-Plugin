using xTestPlatform.Core.Engine;

namespace CAN.Runtime;

/// <summary>尚不知道连接/任务名时也拥有真实求值；同一启动步骤不能与未退出的旧求值重叠。</summary>
internal sealed class CanCyclicPreparationGate : IDisposable
{
    private const string ResourceKey = "CAN.CyclicPreparationGate";
    private readonly object _gate = new();
    private readonly Dictionary<string, Lease> _active = new(StringComparer.Ordinal);
    private bool _retired;

    internal static Lease Enter(IResourceRegistry resources, string key, int timeoutMs, CancellationToken caller)
    {
        if (!resources.TryGet<CanCyclicPreparationGate>(ResourceKey, out var gate))
        {
            var created = new CanCyclicPreparationGate();
            if (resources.TryAdd(ResourceKey, created, ResourceLifetime.Execution)) gate = created;
            else
            {
                created.Dispose();
                if (!resources.TryGet<CanCyclicPreparationGate>(ResourceKey, out gate)) throw new InvalidOperationException("无法登记 CAN 周期启动求值所有权");
            }
        }
        if (gate == null) throw new InvalidOperationException("CAN 周期启动求值所有权为空");
        lock (gate._gate)
        {
            caller.ThrowIfCancellationRequested();
            if (gate._retired) throw new InvalidOperationException("CAN 周期启动作用域已回收");
            if (gate._active.ContainsKey(key)) throw new InvalidOperationException("此周期启动步骤的旧表达式仍未退出，暂不能重新启动");
            var lease = new Lease(gate, key, timeoutMs, caller); gate._active.Add(key, lease); return lease;
        }
    }

    public void Dispose()
    {
        Lease[] active;
        lock (_gate) { _retired = true; active = _active.Values.ToArray(); }
        foreach (var lease in active) lease.Dispose();
    }

    internal sealed class Lease : IDisposable
    {
        private readonly CanCyclicPreparationGate _owner;
        private readonly string _key;
        private readonly CancellationTokenSource _stop;
        private int _disposed;
        internal CanCyclicEvaluationScope Evaluations { get; }
        internal CancellationToken Token { get; }
        internal Lease(CanCyclicPreparationGate owner, string key, int timeoutMs, CancellationToken caller)
        {
            _owner = owner; _key = key; Evaluations = new(timeoutMs);
            _stop = CancellationTokenSource.CreateLinkedTokenSource(caller); Token = _stop.Token;
        }
        internal void Check()
        {
            Token.ThrowIfCancellationRequested();
            lock (_owner._gate)
                if (_owner._retired || Volatile.Read(ref _disposed) != 0)
                    throw new InvalidOperationException("CAN 周期启动求值已失效，禁止访问旧上下文");
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            var cancellation = _stop.CancelAsync();
            _ = Task.WhenAll(cancellation, Evaluations.DrainAsync()).ContinueWith(task =>
            {
                _ = task.Exception;
                lock (_owner._gate)
                    if (_owner._active.TryGetValue(_key, out var current) && ReferenceEquals(current, this)) _owner._active.Remove(_key);
                _stop.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
