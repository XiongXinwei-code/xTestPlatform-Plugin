using xTestPlatform.Core.Engine;

namespace NiDaq.Helpers;

/// <summary>前台等待成功后才发布；后台原生/导出任务不得持有发布回调。</summary>
internal static class DaqResultPublisher
{
    internal static void Publish(IExecutionContext context, string variable, object value,
        CancellationToken caller, DaqTaskResource? owner = null, string? taskName = null)
    {
        caller.ThrowIfCancellationRequested();
        if (owner != null && taskName != null) NiDaqTaskRegistry.EnsureCurrent(context, taskName, owner, caller);
        else owner?.EnsureReady();
        context.SetVariable(variable, value);
        caller.ThrowIfCancellationRequested();
        if (owner != null && taskName != null) NiDaqTaskRegistry.EnsureCurrent(context, taskName, owner, caller);
    }
}
