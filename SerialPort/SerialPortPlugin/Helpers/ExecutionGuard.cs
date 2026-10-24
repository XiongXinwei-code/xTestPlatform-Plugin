using PluginCommon;
using xTestPlatform.Core.Engine;

namespace SerialPort.Helpers;

/// <summary>保留原调用方取消语义，同时冻结平台回收后的旧步骤。</summary>
internal sealed class ExecutionGuard
{
    private readonly ExecutionEpoch _epoch;
    private readonly CancellationToken _caller;
    private ExecutionGuard(IResourceRegistry resources, CancellationToken caller)
    {
        _caller = caller;
        caller.ThrowIfCancellationRequested();
        _epoch = ExecutionEpoch.Capture(resources, "SerialPort");
    }
    internal static ExecutionGuard Capture(IResourceRegistry resources, CancellationToken caller) => new(resources, caller);
    internal void ThrowIfExpired() { _caller.ThrowIfCancellationRequested(); _epoch.ThrowIfExpired(); }
}
