using PluginCommon;
using xTestPlatform.Core.Engine;

namespace VISA.Helpers;

/// <summary>捕获原 caller 与 SDK 执行代次，不将关闭或已发送报文冒充撤回。</summary>
internal sealed class ExecutionGuard
{
    private readonly ExecutionEpoch _epoch;
    private readonly CancellationToken _caller;
    private ExecutionGuard(IResourceRegistry resources, CancellationToken caller)
    {
        _caller = caller;
        caller.ThrowIfCancellationRequested();
        _epoch = ExecutionEpoch.Capture(resources, "VISA");
    }
    internal static ExecutionGuard Capture(IResourceRegistry resources, CancellationToken caller) => new(resources, caller);
    internal void ThrowIfExpired() { _caller.ThrowIfCancellationRequested(); _epoch.ThrowIfExpired(); }
}
