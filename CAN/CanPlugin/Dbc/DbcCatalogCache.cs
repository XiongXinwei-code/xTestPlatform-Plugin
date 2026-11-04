using CAN.Runtime;
using PluginCommon;
using xTestPlatform.Core.Engine;

namespace CAN.Dbc;

/// <summary>仅本次执行的有界目录缓存，按文本指纹复用，不持有 CAN 句柄。</summary>
internal sealed class DbcCatalogCache : IDisposable
{
    private const string ResourceKey = "CAN_DBC_CatalogCache";
    private readonly object _gate = new();
    private readonly Dictionary<string, DbcDatabase> _catalogs = [];
    private bool _disposed;

    internal static async Task<(DbcDatabase Database, string Fingerprint)> LoadAsync(
        DbcDocumentSetting document, IExecutionContext context, CanExecutionGuard guard, CancellationToken ct)
    {
        var resources = context.Resources;
        var epoch = ExecutionEpoch.Capture(resources, "CAN");
        guard.Check();
        if (!resources.TryGet<DbcCatalogCache>(ResourceKey, out var cache))
        {
            var candidate = new DbcCatalogCache();
            if (resources.TryAdd(ResourceKey, candidate, ResourceLifetime.Execution)) cache = candidate;
            else
            {
                candidate.Dispose();
                if (!resources.TryGet<DbcCatalogCache>(ResourceKey, out cache)) throw new InvalidOperationException("DBC 目录缓存登记失败");
            }
        }
        if (cache == null) throw new InvalidOperationException("DBC 目录缓存登记失败");
        epoch.ThrowIfExpired(); ct.ThrowIfCancellationRequested();
        var result = await DbcSharedCatalog.LoadAsync(document, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested(); epoch.ThrowIfExpired(); guard.Check();
        lock (cache._gate)
        {
            cache.Check(); ct.ThrowIfCancellationRequested(); epoch.ThrowIfExpired();
            if (cache._catalogs.TryGetValue(result.Fingerprint, out var existing))
                return (existing, result.Fingerprint);
            if (cache._catalogs.Count >= 16) throw new InvalidOperationException("本次执行已缓存 16 份不同 DBC");
            cache._catalogs.Add(result.Fingerprint, result.Database);
        }
        return result;
    }
    private void Check() { if (_disposed) throw new InvalidOperationException("DBC 目录所属执行已结束，不能复用旧目录"); }
    public void Dispose() { lock (_gate) { _disposed = true; _catalogs.Clear(); } }
}
