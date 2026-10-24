using System.Runtime.CompilerServices;
using xTestPlatform.Core.Engine;

namespace NiDaq.Helpers;

/// <summary>
/// 任务名称是资源键而非变量；任务只属于当前执行资源表，不能销毁其他序列的资源。
/// </summary>
public static class NiDaqTaskRegistry
{
    private static readonly ConditionalWeakTable<object, object> Locks = new();
    private static string Key(string name) => $"NiDaq.Task:{name}";
    public static DaqTaskResource Get(IExecutionContext context, string name)
        => context.Resources.TryGet<DaqTaskResource>(Key(name), out var value) ? value : throw new InvalidOperationException($"未找到当前序列的 DAQ 任务 '{name}'");
    public static void EnsureAbsent(IExecutionContext context, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("任务名称不能为空");
        if (context.Resources.TryGet<object>(Key(name), out _)) throw new InvalidOperationException($"DAQ 任务 '{name}' 已存在，请先成功停止原任务");
    }
    internal static async Task WaitUntilAbsentAsync(IExecutionContext context, string name, int timeoutMs, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("任务名称不能为空");
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            object? captured;
            lock (Locks.GetValue(context.Resources, static _ => new object()))
                if (!context.Resources.TryGet<object>(Key(name), out captured)) return;
            if (captured is not DaqTaskResource previous || (!previous.IsClosed && !previous.IsClosing))
                throw new InvalidOperationException($"DAQ 任务 '{name}' 已存在，请先成功停止原任务；不会接管活动或未知类型资源");
            if (!previous.IsClosed)
                await previous.CloseAsync(DaqTaskFactory.RemainingBudget(started, timeoutMs), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (previous.IsClosed) RemoveClosed(context, name, previous);
        }
    }
    internal static void EnsureCurrent(IExecutionContext context, string name, DaqTaskResource expected, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (!context.Resources.TryGet<DaqTaskResource>(Key(name), out var current) || !ReferenceEquals(current, expected))
            throw new InvalidOperationException($"DAQ 任务 '{name}' 的资源代次已改变，禁止发布旧任务结果");
        caller.ThrowIfCancellationRequested();
        expected.EnsureReady();
    }
    public static void Register(IExecutionContext context, string name, DaqTaskResource resource)
    {
        lock (Locks.GetValue(context.Resources, static _ => new object()))
            if (!context.Resources.TryAdd(Key(name), resource, ResourceLifetime.Execution)) throw new InvalidOperationException($"DAQ 任务 '{name}' 已被其他步骤注册");
    }
    public static void RemoveClosed(IExecutionContext context, string name, DaqTaskResource expected)
    {
        lock (Locks.GetValue(context.Resources, static _ => new object()))
            if (expected.IsClosed && context.Resources.TryGet<DaqTaskResource>(Key(name), out var current) && ReferenceEquals(current, expected)) context.Resources.Remove(Key(name), dispose: false);
    }
}
