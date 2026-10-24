using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CAN.Runtime;

/// <summary>在资源表查找之前取得连接名锁，覆盖 Open/Close 整个注册与移除操作。</summary>
internal static class CanRegistryGuard
{
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<string, SemaphoreSlim>> Gates = new();
    public static async Task<IDisposable> EnterAsync(object registry, string name, CancellationToken ct, int timeoutMs = 5000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        var gate = Gates.GetValue(registry, _ => new(StringComparer.OrdinalIgnoreCase)).GetOrAdd(name, _ => new(1, 1));
        if (!await gate.WaitAsync(timeoutMs, ct).ConfigureAwait(false)) throw new TimeoutException($"等待 CAN 连接 {name} 生命周期操作超过 {timeoutMs} ms");
        return new Lease(gate);
    }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int _released;
        public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) gate.Release(); }
    }
}
