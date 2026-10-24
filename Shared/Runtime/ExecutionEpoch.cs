using xTestPlatform.Core.Engine;

namespace PluginCommon;

/// <summary>
/// Captures the current SDK execution resource scope before an asynchronous admission
/// wait. A step token and a resource scope are different lifetimes: SDK cleanup must
/// also prevent a still-waiting old step from registering/opening a new resource.
/// This private marker owns no hardware and never blocks IDisposable.
/// </summary>
internal sealed class ExecutionEpoch : IDisposable
{
    private readonly IResourceRegistry _resources;
    private readonly string _module;
    private readonly string _key;
    private int _retired;

    private ExecutionEpoch(IResourceRegistry resources, string module, string key)
        => (_resources, _module, _key) = (resources, module, key);

    internal static ExecutionEpoch Capture(IResourceRegistry resources, string module)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        string key = "PluginCommon.ExecutionEpoch:" + module;
        if (resources.TryGet<ExecutionEpoch>(key, out var existing))
        {
            existing.ThrowIfExpired();
            return existing;
        }
        var created = new ExecutionEpoch(resources, module, key);
        if (resources.TryAdd(key, created, ResourceLifetime.Execution))
        {
            created.ThrowIfExpired();
            return created;
        }
        created.Dispose();
        if (resources.TryGet<ExecutionEpoch>(key, out existing))
        {
            existing.ThrowIfExpired();
            return existing;
        }
        throw new InvalidOperationException($"{module} 无法登记执行资源代次，拒绝建立未受生命周期管理的资源");
    }

    /// <summary>
    /// Check after admission/retirement waits, before and after registration, before
    /// actual submission and before reporting completion. Keep this captured instance;
    /// capturing again after an await would silently switch to the next execution.
    /// Register hardware owners before I/O and dispose only the captured staged owner
    /// if a post-registration check fails. Never hold this marker's lock over SDK APIs.
    /// </summary>
    internal void ThrowIfExpired()
    {
        if (Volatile.Read(ref _retired) != 0 ||
            !_resources.TryGet<ExecutionEpoch>(_key, out var current) || !ReferenceEquals(current, this))
        {
            // SDK may remove entries before calling their IDisposable callbacks. The
            // identity check is therefore essential even when _retired is still zero.
            Interlocked.Exchange(ref _retired, 1);
            throw new InvalidOperationException($"{_module} 当前执行资源作用域已回收或被新代次取代，禁止旧步骤迟到建立、登记或报告成功");
        }
    }

    public void Dispose() => Interlocked.Exchange(ref _retired, 1);
}
