using System.Diagnostics;

namespace OpcUa.Helpers;

/// <summary>清理拥有实际 Create/Delete 的退出期限；调用方超时后活动租约仍保留到真实收尾结束。</summary>
public static class OpcUaSubscriptionCleanup
{
    public static Task Start(Task? creation, Func<CancellationToken, Task> delete, Action removeLocal,
        IDisposable? activity, Action<Exception> report, int deleteTimeoutMs = 2000)
    {
        var task = Task.Run(async () =>
        {
            var failures = new List<Exception>();
            try
            {
                if (creation != null)
                {
                    try { await creation.ConfigureAwait(false); }
                    catch (Exception ex) { failures.Add(ex); }
                }
                var deadline = new OpcUaRequestOperation(deleteTimeoutMs, "OPC UA 订阅删除", default, default);
                try { deadline.ThrowIfActive(); await delete(deadline.Token).ConfigureAwait(false); }
                catch (Exception ex) { failures.Add(ex); }
                finally
                {
                    try { await deadline.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception ex) { failures.Add(ex); }
                }
                try { removeLocal(); }
                catch (Exception ex) { failures.Add(ex); }
            }
            finally { activity?.Dispose(); }
            if (failures.Count > 0) throw new AggregateException("OPC UA 订阅创建/清理存在未成功阶段", failures);
        }, CancellationToken.None);
        _ = task.ContinueWith(t =>
        {
            var error = t.Exception!;
            Trace.TraceError($"OPC UA 订阅收尾失败：{error}");
            try { report(error); }
            catch (Exception callbackError) { Trace.TraceError($"OPC UA 订阅诊断回调失败：{callbackError}"); }
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }
}
