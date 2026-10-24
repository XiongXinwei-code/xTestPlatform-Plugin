using System.Diagnostics;
using System.Runtime.CompilerServices;
using CAN.Adapters;
using CAN.Models;
using PluginCommon;

namespace CAN.Runtime;

/// <summary>UDS/XCP 共用完整事务门；超时不释放尚未实际退出的事务。</summary>
internal static class CanProtocolTransaction
{
    private sealed class State
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal string? Isolation;
    }
    private sealed record Budget(long Start, int TimeoutMs, CancellationToken Token)
    {
        internal int Remaining()
        {
            Token.ThrowIfCancellationRequested();
            int left = TimeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(Start).TotalMilliseconds);
            return left > 0 ? left : throw new TimeoutException($"协议事务超过配置总期限 {TimeoutMs} ms，禁止后续提交");
        }
    }
    private static readonly ConditionalWeakTable<ICanAdapter, State> States = new();
    private static readonly AsyncLocal<Budget?> Active = new();
    internal static int Remaining(int maximum) => Active.Value is { } budget ? Math.Min(maximum, budget.Remaining()) : maximum;
    internal static void Invalidate(ICanAdapter adapter, string reason) => Interlocked.CompareExchange(ref States.GetValue(adapter, _ => new()).Isolation, reason, null);

    internal static async Task<T> RunAsync<T>(ICanAdapter adapter, int timeoutMs, Func<CancellationToken, Task<T>> operation, CancellationToken caller)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs); caller.ThrowIfCancellationRequested();
        long started = Stopwatch.GetTimestamp(); var state = States.GetValue(adapter, _ => new());
        if (!await state.Gate.WaitAsync(timeoutMs, caller).ConfigureAwait(false))
            throw new TimeoutException($"等待协议事务超过 {timeoutMs} ms，未提交请求");
        Task<T>? actual = null;
        try
        {
            caller.ThrowIfCancellationRequested();
            if (Volatile.Read(ref state.Isolation) is { } isolation) throw new InvalidOperationException($"原始协议连接已隔离，请关闭并重新打开：{isolation}");
            int remaining = new Budget(started, timeoutMs, caller).Remaining();
            actual = Task.Run(async () =>
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
                var budget = new Budget(started, timeoutMs, deadline.Token);
                deadline.CancelAfter(budget.Remaining());
                Active.Value = budget;
                try
                {
                    budget.Remaining();
                    var result = await operation(deadline.Token).ConfigureAwait(false);
                    budget.Remaining();
                    return result;
                }
                finally { Active.Value = null; }
            }, CancellationToken.None);
            try
            {
                var result = await actual.WaitAsync(TimeSpan.FromMilliseconds(remaining), caller).ConfigureAwait(false);
                new Budget(started, timeoutMs, caller).Remaining();
                return result;
            }
            catch (Exception ex)
            {
                Invalidate(adapter, ex.Message);
                if (ex is OperationCanceledException && !caller.IsCancellationRequested)
                    throw new TimeoutException($"协议事务超过配置总期限 {timeoutMs} ms（含排队、发送及全部响应）", ex);
                if (ex is TimeoutException)
                    throw new TimeoutException($"CAN 协议通信等待超时；事务总预算 {timeoutMs} ms（含排队、发送及全部响应）：{ex.Message}", ex);
                throw;
            }
        }
        finally
        {
            if (actual == null || actual.IsCompleted) { _ = actual?.Exception; state.Gate.Release(); }
            else _ = actual.ContinueWith(t => { _ = t.Exception; state.Gate.Release(); }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}

/// <summary>本次接收窗口到期；不表示底层读取已退出，也不是整个协议事务的期限。</summary>
internal sealed class CanReceiveTimeoutException(int timeoutMs, Exception inner)
    : TimeoutException($"CAN 接收窗口 {timeoutMs} ms 超时；实际读取退出状态以连接诊断为准，不能继续复用本次协议连接。{inner.Message}", inner);

/// <summary>协议使用既有 owner；直接库调用的旧适配器也保留实际 I/O 门，不产生接收泵。</summary>
internal static class CanRawIo
{
    private static readonly ConditionalWeakTable<ICanAdapter, PendingIoGate> Fallback = new();
    internal static async Task WriteAsync(ICanAdapter adapter, CanMessage frame, int timeoutMs, CancellationToken ct)
    {
        int remaining = CanProtocolTransaction.Remaining(timeoutMs);
        if (adapter is CanConnectionResource owner) await owner.WriteAsync(frame, remaining, ct).ConfigureAwait(false);
        else
        {
            var copy = CanReceiveRouter.Copy(frame);
            await Fallback.GetValue(adapter, _ => new()).RunBlockingAsync(() => { CanProtocolTransaction.Remaining(remaining); ct.ThrowIfCancellationRequested(); adapter.Write(copy); }, remaining, "CAN 协议发送", ct).ConfigureAwait(false);
        }
        ct.ThrowIfCancellationRequested();
        CanProtocolTransaction.Remaining(int.MaxValue);
    }
    internal static async Task<CanMessage?> ReadAsync(ICanAdapter adapter, uint? id, int timeoutMs, CancellationToken ct)
    {
        int remaining = CanProtocolTransaction.Remaining(timeoutMs);
        try
        {
            CanMessage? message;
            if (adapter is CanConnectionResource owner)
                message = await owner.ReadAsync(id, remaining, ct).ConfigureAwait(false);
            else
                message = await Fallback.GetValue(adapter, _ => new()).RunBlockingAsync(() =>
                {
                    int left = CanProtocolTransaction.Remaining(remaining); ct.ThrowIfCancellationRequested();
                    return id.HasValue ? adapter.Read(id.Value, left, ct) : adapter.Read(left, ct);
                }, remaining, "CAN 协议接收", ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            CanProtocolTransaction.Remaining(int.MaxValue);
            // 同一被截短的读取，无论驱动先返回 null 还是软等待先抛错，都属于总事务限制。
            if (message == null && remaining < timeoutMs)
                throw new TimeoutException("CAN 接收受协议事务剩余总预算限制，未在剩余期限内取得响应");
            return message;
        }
        catch (TimeoutException ex) when (remaining == timeoutMs)
        {
            // 若总事务预算已耗尽，仍交给事务层报告；不能误称只是 ECU 响应窗口到期。
            // 原始任务与隔离由 owner / fallback gate 继续持有，此处只统一结果分类。
            ct.ThrowIfCancellationRequested();
            CanProtocolTransaction.Remaining(int.MaxValue);
            throw new CanReceiveTimeoutException(timeoutMs, ex);
        }
    }
}
