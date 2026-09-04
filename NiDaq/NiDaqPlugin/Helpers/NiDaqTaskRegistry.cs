using System.Collections.Concurrent;
using DaqTask = NationalInstruments.DAQmx.Task;

namespace NiDaq.Helpers;

/// <summary>
/// NI DAQ 任务注册表：按任务名称在进程内共享 DaqTask 实例。
/// 任务名称是普通字符串（可由表达式求值得到），不是平台变量路径，
/// 因此不能使用 IExecutionContext 的变量存取来保存任务对象。
/// </summary>
public static class NiDaqTaskRegistry
{
    private static readonly ConcurrentDictionary<string, DaqTask> Tasks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, object?> Metadata = new(StringComparer.Ordinal);

    public static DaqTask? Get(string taskName)
        => Tasks.TryGetValue(taskName, out var task) ? task : null;

    public static void Set(string taskName, DaqTask task)
        => Tasks[taskName] = task;

    /// <summary>移除并返回已注册的任务（不销毁）。</summary>
    public static DaqTask? Remove(string taskName)
    {
        foreach (var key in Metadata.Keys)
        {
            if (key.StartsWith(taskName + "_", StringComparison.Ordinal))
                Metadata.TryRemove(key, out _);
        }
        return Tasks.TryRemove(taskName, out var task) ? task : null;
    }

    public static void SetMetadata(string taskName, string key, object? value)
        => Metadata[$"{taskName}_{key}"] = value;

    public static object? GetMetadata(string taskName, string key)
        => Metadata.TryGetValue($"{taskName}_{key}", out var value) ? value : null;
}
