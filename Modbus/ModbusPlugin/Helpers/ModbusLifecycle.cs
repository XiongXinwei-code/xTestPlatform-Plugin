using xTestPlatform.Core.Engine;
using PluginCommon;

namespace Modbus.Helpers;

public static class ModbusLifecycle
{
    public static Task OpenRegisteredAsync(IExecutionContext context, string name, ModbusConnectionResource owner, Func<ModbusConnectionResource, CancellationToken, Task> open, CancellationToken ct)
        => OpenRegisteredAsync(context, name, owner, open, ct, ExecutionGuard.Capture(context.Resources, ct).ThrowIfExpired);

    internal static async Task OpenRegisteredAsync(IExecutionContext context, string name, ModbusConnectionResource owner, Func<ModbusConnectionResource, CancellationToken, Task> open, CancellationToken ct, Action ensureExecutionCurrent)
    {
        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        var key = ModbusHelper.GetConnectionKey(name);
        bool registered = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            ensureExecutionCurrent();
            long generation;
            ModbusConnectionResource? previous = null;
            lock (ModbusHelper.RegistryLock(context.Resources))
            {
                generation = ModbusHelper.GetCloseGeneration(context.Resources, key);
                if (context.Resources.TryGet<object>(key, out var existing))
                {
                    if (existing is not ModbusConnectionResource candidate || (!candidate.IsClosing && !candidate.IsClosed))
                        throw new InvalidOperationException($"Modbus 连接 {name} 已存在且仍活动，请先断开");
                    previous = candidate;
                }
            }
            if (previous != null && !previous.IsClosed)
                await previous.CloseAsync(ModbusHelper.RemainingBudget(startedAt, owner.TimeoutMs), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            ensureExecutionCurrent();
            lock (ModbusHelper.RegistryLock(context.Resources))
            {
                ensureExecutionCurrent();
                ModbusHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                if (previous != null && context.Resources.TryGet<ModbusConnectionResource>(key, out var current) && ReferenceEquals(current, previous))
                {
                    if (!previous.IsClosed) throw new InvalidOperationException("旧 Modbus 连接尚未确认释放，不能重新登记");
                    context.Resources.Remove(key, dispose: false);
                }
                if (!context.Resources.TryAdd(key, owner, ResourceLifetime.Execution)) throw new InvalidOperationException($"Modbus 连接 {name} 已存在，请先成功关闭");
                registered = true;
                ensureExecutionCurrent();
            }
            await owner.InitializeAsync((current, token) => { ensureExecutionCurrent(); return open(current, token); }, ct, ModbusHelper.RemainingBudget(startedAt, owner.TimeoutMs)).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            ensureExecutionCurrent();
        }
        catch (Exception openingError)
        {
            // 只停止本次等待；实际打开及收尾继续由原 owner 持有。
            bool callerCancelled = openingError is OperationCanceledException && ct.IsCancellationRequested;
            if (callerCancelled || owner.IsClosing)
            {
                try { owner.Dispose(); }
                catch (Exception cleanupError)
                {
                    if (callerCancelled) throw new OperationCanceledException("Modbus 建立已中断，后台收尾未确认，保留原 owner", new AggregateException(openingError, cleanupError), ct);
                    throw new AggregateException("Modbus 建立所属资源已退出，后台收尾未确认，保留原 owner", openingError, cleanupError);
                }
                // Retiring the foreground waiter must not await the same native
                // close again. The registered owner and retirement keep real I/O.
                if (registered) RemoveAfterActualClose(context, key, owner);
                throw;
            }
            try
            {
                owner.Dispose();
                if (!owner.IsClosed) await owner.CloseAsync(ModbusHelper.RemainingBudget(startedAt, owner.TimeoutMs), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupError) { throw new AggregateException("Modbus 建立失败且清理未确认，保留 owner，请显式 Disconnect 重试", openingError, cleanupError); }
            if (registered)
                lock (ModbusHelper.RegistryLock(context.Resources))
                    if (context.Resources.TryGet<ModbusConnectionResource>(key, out var current) && ReferenceEquals(current, owner)) context.Resources.Remove(key);
            throw;
        }
    }

    private static void RemoveAfterActualClose(IExecutionContext context, string key, ModbusConnectionResource expected)
    {
        void RemoveExactOwner()
        {
            lock (ModbusHelper.RegistryLock(context.Resources))
                if (context.Resources.TryGet<ModbusConnectionResource>(key, out var current) && ReferenceEquals(current, expected)) context.Resources.Remove(key);
        }
        if (expected.IsClosed) { RemoveExactOwner(); return; }
        if (expected.CloseCompletion is not { } actualClose) return;
        var removal = actualClose.ContinueWith(_ => RemoveExactOwner(), CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        _ = removal.ContinueWith(task => System.Diagnostics.Trace.TraceError($"Modbus 旧连接登记移除失败，保留精确所有者诊断：{task.Exception}"),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }
}
