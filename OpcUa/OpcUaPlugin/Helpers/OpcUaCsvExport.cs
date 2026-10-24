namespace OpcUa.Helpers;

/// <summary>同一路径的实际导出串行化；失败可能已部分写入，不能靠重试整批猜测文件状态。</summary>
internal static class OpcUaCsvExport
{
    private sealed class PathState
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal int Users;
        internal UnknownWrite? Unknown;
    }
    private sealed record UnknownWrite(long FirstSequence, long LastSequence, Exception Error);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, PathState> Paths = new(StringComparer.OrdinalIgnoreCase);

    internal static async Task WriteAsync(string path, IReadOnlyList<DataAcqRecord> records,
        OpcUaConsumptionQueue.Consumption operation, Func<string, Task> write)
    {
        string fullPath = Path.GetFullPath(path);
        PathState state;
        lock (Sync)
        {
            if (!Paths.TryGetValue(fullPath, out state!)) Paths.Add(fullPath, state = new());
            state.Users++;
        }
        bool entered = false;
        try
        {
            if (!await state.Gate.WaitAsync(operation.Remaining, operation.Token).ConfigureAwait(false))
                throw new TimeoutException("等待同路径 CSV 实际导出超过消费总期限，尚未提交本批写入");
            entered = true;
            operation.ThrowIfSubmissionStopped();
            lock (Sync)
                if (state.Unknown is { } previous) throw UnknownError(fullPath, previous, retry: true);
            try { await write(fullPath).ConfigureAwait(false); }
            catch (Exception ex)
            {
                var unknown = new UnknownWrite(records.Count == 0 ? 0 : records[0].Sequence,
                    records.Count == 0 ? 0 : records[^1].Sequence, ex);
                lock (Sync) state.Unknown = unknown;
                throw UnknownError(fullPath, unknown, retry: false);
            }
            operation.MarkExternalOutputCommitted();
        }
        finally
        {
            if (entered) state.Gate.Release();
            lock (Sync)
            {
                state.Users--;
                // Successful paths do not accumulate for the lifetime of a long-running host.
                // Unknown paths deliberately survive acquisition replacement in this plugin process.
                if (state.Users == 0 && state.Unknown == null)
                { Paths.Remove(fullPath); state.Gate.Dispose(); }
            }
        }
    }

    private static IOException UnknownError(string path, UnknownWrite unknown, bool retry) => new(
        $"CSV 写入结果未知{(retry ? "，该路径已隔离，未重新写入" : "，可能存在部分追加，已隔离该路径")}：{path}；" +
        $"受影响记录 Sequence={unknown.FirstSequence}..{unknown.LastSequence}；FIFO 保留，不自动回滚或重复导出。" +
        "请保留并检查原文件，选择新的 CSV 文件路径后重试；重启进程不代表旧文件已经完整或可安全追加。", unknown.Error);
}
