using System.Collections.Concurrent;
using xTestPlatform.Core.Engine;

namespace CiA402.Core;

/// <summary>仅串行化同名轴的绑定/解绑，防止旧解绑误移除新会话；不持有父总线。</summary>
internal static class AxisBindingGate
{
    private const string ResourceKey = "CiA402.AxisLifecycleGates";
    private sealed class Gates
    {
        public ConcurrentDictionary<string, SemaphoreSlim> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<IDisposable> EnterAsync(IExecutionContext context, string axisKey, CancellationToken ct)
    {
        if (!context.Resources.TryGet<Gates>(ResourceKey, out var gates))
        {
            var candidate = new Gates();
            if (context.Resources.TryAdd(ResourceKey, candidate)) gates = candidate;
            else if (!context.Resources.TryGet<Gates>(ResourceKey, out gates))
                throw new InvalidOperationException("CiA402 轴生命周期资源重名或注册失败");
        }
        if (gates == null) throw new InvalidOperationException("CiA402 轴生命周期资源不存在");
        var gate = gates.Items.GetOrAdd(axisKey, static _ => new(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int _released;
        public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) gate.Release(); }
    }
}
