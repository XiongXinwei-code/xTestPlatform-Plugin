using xTestPlatform.Core.Engine;

namespace Modbus.Helpers;

/// <summary>外层续体可能迟于真实 I/O 退出；发布前再次核对调用方和原连接代次。</summary>
internal static class ModbusResultPublisher
{
    internal static void EnsureActive(IExecutionContext context, string key, ModbusConnectionResource owner, CancellationToken caller)
    {
        EnsureCallerActive(context, caller);
        if (!context.Resources.TryGet<ModbusConnectionResource>(key, out var current) || !ReferenceEquals(current, owner))
            throw new InvalidOperationException("Modbus 连接代次已经变更，拒绝发布旧连接结果");
        owner.EnsureAvailableForPublication();
        caller.ThrowIfCancellationRequested();
    }

    internal static void EnsureCallerActive(IExecutionContext context, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (context.IsAbortRequested) throw new InvalidOperationException("序列已经请求中断，拒绝发布迟到的 Modbus 结果");
    }
}
