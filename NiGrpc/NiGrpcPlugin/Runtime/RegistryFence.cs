using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Engine;

namespace NiGrpc.Runtime;

internal static class RegistryFence
{
    private static readonly ConditionalWeakTable<IResourceRegistry, ConcurrentDictionary<string, SemaphoreSlim>> Gates = new();
    internal static SemaphoreSlim Get(IResourceRegistry registry, string key) =>
        Gates.GetValue(registry, _ => new(StringComparer.Ordinal)).GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

    internal static async Task RemoveAfterCloseAsync(IResourceRegistry registry, string key, RemoteConnection owner, int timeoutMs)
    {
        // 前台超时只结束等待；后台一直观察实际关闭任务，直到可移除同一实例。
        try { await owner.RequestClose(timeoutMs).ConfigureAwait(false); }
        finally
        {
            if (owner.LocalChannelClosed)
            {
                var gate = Get(registry, key);
                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (registry.TryGet<RemoteConnection>(key, out var current) && ReferenceEquals(current, owner))
                        registry.Remove(key, false);
                }
                finally { gate.Release(); }
            }
        }
    }
}
