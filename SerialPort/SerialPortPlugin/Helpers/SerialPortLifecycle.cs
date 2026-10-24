using xTestPlatform.Core.Engine;
using PluginCommon;

namespace SerialPort.Helpers;

public static class SerialPortLifecycle
{
    public static Task OpenRegisteredAsync(IExecutionContext context, string name, SerialConnectionResource resource, Action open, CancellationToken ct, int timeoutMs = 5000)
        => OpenRegisteredAsync(context, name, resource, open, ct, timeoutMs, ExecutionGuard.Capture(context.Resources, ct).ThrowIfExpired);

    internal static async Task OpenRegisteredAsync(IExecutionContext context, string name, SerialConnectionResource resource, Action open, CancellationToken ct, int timeoutMs, Action ensureExecutionCurrent)
    {
        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var key = SerialPortHelper.GetPortKey(name);
        bool registered = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            ensureExecutionCurrent();
            long generation;
            SerialConnectionResource? previous = null;
            lock (SerialPortHelper.RegistryLock(context.Resources))
            {
                generation = SerialPortHelper.GetCloseGeneration(context.Resources, key);
                if (context.Resources.TryGet<object>(key, out var existing))
                {
                    if (existing is not SerialConnectionResource candidate || (!candidate.IsClosing && !candidate.IsClosed))
                        throw new InvalidOperationException($"串口 {name} 已存在且仍活动，请先关闭");
                    previous = candidate;
                }
            }
            if (previous != null && !previous.IsClosed)
                await previous.CloseAsync(SerialPortHelper.RemainingBudget(startedAt, timeoutMs), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            ensureExecutionCurrent();
            lock (SerialPortHelper.RegistryLock(context.Resources))
            {
                ensureExecutionCurrent();
                SerialPortHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                if (previous != null && context.Resources.TryGet<SerialConnectionResource>(key, out var current) && ReferenceEquals(current, previous))
                {
                    if (!previous.IsClosed) throw new InvalidOperationException("旧串口连接尚未确认释放，不能重新登记");
                    context.Resources.Remove(key, dispose: false);
                }
                if (!context.Resources.TryAdd(key, resource, ResourceLifetime.Execution)) throw new InvalidOperationException($"串口 {name} 已存在，请先成功关闭");
                registered = true;
                ensureExecutionCurrent();
            }
            await resource.OpenAsync(() => { ensureExecutionCurrent(); open(); }, SerialPortHelper.RemainingBudget(startedAt, timeoutMs), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            ensureExecutionCurrent();
        }
        catch (Exception openingError)
        {
            if (openingError is OperationCanceledException && ct.IsCancellationRequested)
            {
                try { resource.Dispose(); }
                catch (Exception cleanupError) { throw new OperationCanceledException("串口打开已中断，后台收尾未确认，保留原 owner", new AggregateException(openingError, cleanupError), ct); }
                throw;
            }
            try
            {
                resource.Dispose();
                if (!resource.IsClosed) await resource.CloseAsync(SerialPortHelper.RemainingBudget(startedAt, timeoutMs), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupError) { throw new AggregateException("串口打开失败且清理未确认，保留 owner，请显式 Close 重试", openingError, cleanupError); }
            if (registered) RemoveClosed(context, key, resource);
            throw;
        }
    }
    public static void RemoveClosed(IExecutionContext context, string key, SerialConnectionResource expected)
    {
        lock (SerialPortHelper.RegistryLock(context.Resources))
            if (context.Resources.TryGet<SerialConnectionResource>(key, out var current) && ReferenceEquals(current, expected)) context.Resources.Remove(key);
    }
}
