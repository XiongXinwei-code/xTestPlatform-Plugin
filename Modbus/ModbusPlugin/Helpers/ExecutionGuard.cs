using PluginCommon;
using xTestPlatform.Core.Engine;

namespace Modbus.Helpers;

/// <summary>原 caller 优先；SDK 资源回收不是用户取消，不能用新代次续接旧步骤。</summary>
internal sealed class ExecutionGuard
{
    private readonly ExecutionEpoch _epoch;
    private readonly CancellationToken _caller;
    private ExecutionGuard(IResourceRegistry resources, CancellationToken caller)
    {
        _caller = caller;
        caller.ThrowIfCancellationRequested();
        _epoch = ExecutionEpoch.Capture(resources, "Modbus");
    }
    internal static ExecutionGuard Capture(IResourceRegistry resources, CancellationToken caller) => new(resources, caller);
    internal void ThrowIfExpired() { _caller.ThrowIfCancellationRequested(); _epoch.ThrowIfExpired(); }
}
