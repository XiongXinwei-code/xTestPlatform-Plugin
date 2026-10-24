using xTestPlatform.Core.Engine;

namespace SerialPort.Helpers;

/// <summary>禁止已中断步骤或已被替换的串口代次发布迟到结果。</summary>
internal static class SerialResultPublisher
{
    internal static void EnsureActive(IExecutionContext context, string key, SerialConnectionResource owner, CancellationToken caller)
    {
        EnsureCallerActive(context, caller);
        if (!context.Resources.TryGet<SerialConnectionResource>(key, out var current) || !ReferenceEquals(current, owner))
            throw new InvalidOperationException("串口连接代次已经变更，拒绝发布旧连接结果");
        owner.EnsureAvailableForPublication();
        caller.ThrowIfCancellationRequested();
    }

    internal static void EnsureCallerActive(IExecutionContext context, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (context.IsAbortRequested) throw new InvalidOperationException("序列已经请求中断，拒绝发布迟到的串口结果");
    }
}
