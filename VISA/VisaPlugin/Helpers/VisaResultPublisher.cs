using xTestPlatform.Core.Engine;

namespace VISA.Helpers;

/// <summary>真实 I/O 退出并不保证等待它的步骤续体仍属于当前执行。</summary>
internal static class VisaResultPublisher
{
    internal static void EnsureActive(IExecutionContext context, string key, VisaSessionResource owner, CancellationToken caller)
    {
        EnsureCallerActive(context, caller);
        if (!context.Resources.TryGet<VisaSessionResource>(key, out var current) || !ReferenceEquals(current, owner))
            throw new InvalidOperationException("VISA 会话代次已经变更，拒绝发布旧会话结果");
        owner.EnsureAvailableForPublication();
        caller.ThrowIfCancellationRequested();
    }

    internal static void EnsureCallerActive(IExecutionContext context, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (context.IsAbortRequested) throw new InvalidOperationException("序列已经请求中断，拒绝发布迟到的 VISA 结果");
    }
}
