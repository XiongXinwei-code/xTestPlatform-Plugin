using System.Diagnostics;

namespace CAN.Runtime;

public sealed partial class CanConnectionResource
{
    private sealed class ListenerJob(string name, string key, string kind, CanLatestSubscription subscription)
    {
        internal readonly string Name = name, Key = key, Kind = kind;
        internal readonly CanLatestSubscription Subscription = subscription;
        internal CanBackgroundJob Job = null!;
        internal bool StopRequested;
        internal string? SourceError;
        internal long Publications;
        internal DateTimeOffset Started = DateTimeOffset.UtcNow;
    }
    private readonly Dictionary<string, ListenerJob> _listenerJobs = new(StringComparer.Ordinal);

    // 进入本方法即接管 outputs；只有真实工作退出后才释放，调用方不能再 Dispose。
    internal async Task StartBackgroundReadAsync(string name, CanLatestRoute[] routes, int publishIntervalMs,
        int timeoutMs, CancellationToken startupToken, CanBackgroundOutputLease outputs,
        Func<CanLatestSnapshot, CancellationToken, Task> publish, string kind)
    {
        CanLatestSubscription? subscription = null;
        ListenerJob? entry = null;
        bool transferred = false;
        using var setupStop = CancellationTokenSource.CreateLinkedTokenSource(startupToken);
        try
        {
            if (string.IsNullOrWhiteSpace(name) || publishIntervalMs <= 0 || timeoutMs <= 0)
                throw new ArgumentException("监听任务名不能为空，发布间隔和启动期限必须大于 0");
            setupStop.CancelAfter(timeoutMs);
            // 控制模式切换可能等待前一事务，绝不占用调用方 UI 线程。
            await Task.Run(() => EnsureSharedReceive(timeoutMs, setupStop.Token), CancellationToken.None).ConfigureAwait(false);
            outputs.Check(setupStop.Token);
            lock (_gate)
            {
                CheckLocked();
                if (_listenerJobs.TryGetValue(name, out var existing) && !existing.StopRequested)
                    throw new InvalidOperationException($"监听任务 {name} 已存在，请先停止");
                if (_listenerJobs.TryGetValue(name, out existing) && !existing.Job.Completion.IsCompleted)
                    throw new InvalidOperationException($"监听任务 {name} 的旧发布尚未退出");
                if (_listenerJobs.Count >= 256 && !_listenerJobs.ContainsKey(name))
                    throw new InvalidOperationException("此连接监听任务记录已达 256 项，请重开连接后使用");
                subscription = _router.Listen(routes);
                string key = "CAN.Listener:" + Guid.NewGuid().ToString("N");
                entry = new ListenerJob(name, key, kind, subscription);
                var captured = entry;
                var admitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                // 监听不是发送者，不受逐批确认发送者的独占准入限制；仍纳入同一个连接收尾集合。
                entry.Job = new CanBackgroundJob(async ct =>
                {
                    try
                    {
                        await admitted.Task.ConfigureAwait(false);
                        while (true)
                        {
                            outputs.Check(ct);
                            var snapshot = ReadLatest(captured.Subscription);
                            if (!snapshot.Running) throw new InvalidOperationException("CAN 总线监听接收已停止：" + snapshot.Error);
                            await publish(snapshot, ct).ConfigureAwait(false);
                            outputs.Check(ct);
                            Interlocked.Increment(ref captured.Publications);
                            await Task.Delay(publishIntervalMs, ct).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        lock (_gate)
                            if (!captured.StopRequested) captured.SourceError ??= captured.Subscription.Snapshot().Error ?? _router.Diagnostics.Error ?? _isolation;
                        captured.Subscription.Dispose(); outputs.Dispose();
                    }
                });
                _jobs.Add(key, entry.Job);
                transferred = true;
                _listenerJobs[name] = entry;
                try { outputs.Check(setupStop.Token); }
                catch
                {
                    entry.StopRequested = true;
                    entry.Job.Dispose();
                    throw;
                }
                finally { admitted.TrySetResult(); }
            }
        }
        catch (Exception ex)
        {
            if (transferred && entry != null)
            {
                lock (_gate) entry.StopRequested = true;
                RemoveFailedStartupWhenExited(entry.Key, entry.Job);
            }
            else { subscription?.Dispose(); outputs.Dispose(); }
            if (ex is OperationCanceledException && !startupToken.IsCancellationRequested && setupStop.IsCancellationRequested)
                throw new TimeoutException($"CAN 监听启动超过配置期限 {timeoutMs} ms", ex);
            throw;
        }
    }

    internal async Task<string?> StopBackgroundReadAsync(string name, int timeoutMs, CancellationToken ct, bool ignoreIfMissing = false)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        ct.ThrowIfCancellationRequested();
        ListenerJob? entry;
        lock (_gate)
        {
            if (!_listenerJobs.TryGetValue(name, out entry))
            {
                if (ignoreIfMissing) return null;
                throw new InvalidOperationException($"此连接不存在总线监听任务 {name}");
            }
            if (!entry.StopRequested) entry.SourceError ??= entry.Subscription.Snapshot().Error ?? _router.Diagnostics.Error ?? _isolation;
            entry.StopRequested = true;
            entry.Job.Dispose();
        }
        // 允许关闭/故障连接继续收尾。历史接收/发布异常不阻止真实退出后的成功停止。
        await entry.Job.StopAsync(timeoutMs, ct).ConfigureAwait(false);
        lock (_gate)
            if (_jobs.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry.Job)) _jobs.Remove(entry.Key);
        return entry.Job.StopWarning ?? entry.SourceError;
    }

    internal object[] GetBackgroundReadStatuses()
    {
        lock (_gate)
            return _listenerJobs.Values.Select(entry =>
            {
                var snapshot = ReadLatest(entry.Subscription);
                bool exited = entry.Job.Completion.IsCompleted;
                string? error = entry.Job.Error ?? entry.SourceError ?? snapshot.Error ?? (!entry.StopRequested ? _router.Diagnostics.Error ?? _isolation : null);
                string state = error != null ? "Faulted" : exited ? "Stopped" : entry.StopRequested || _closing ? "Stopping" : "Running";
                return (object)new { TaskName = entry.Name, Kind = entry.Kind, State = state,
                    Active = state == "Running", Healthy = state == "Running" && snapshot.Error == null,
                    HostExited = exited, entry.Started, Publications = Interlocked.Read(ref entry.Publications),
                    snapshot.ReceivedFrames, snapshot.Replacements, Error = error,
                    Frames = snapshot.Frames.Select(f => new { Id = $"0x{f.Route.Id:X}", FrameType = f.Route.FrameType.ToString(),
                        FrameFormat = f.Route.Format.ToString(), f.Sequence, f.Valid, f.Quality, f.AgeUpperBoundMs }).ToArray() };
            }).ToArray();
    }

    private CanLatestSnapshot ReadLatest(CanLatestSubscription subscription)
    {
        var snapshot = subscription.Snapshot();
        if (_raw is not ICanClockValidity clock) return snapshot;
        return snapshot with { Frames = snapshot.Frames.Select(frame => frame.Frame != null && !clock.IsClockCurrent(frame.Frame.ClockId)
            ? frame with { Valid = false, Quality = frame.Quality is "Stopped" or "NeverReceived" ? frame.Quality : "TimeUnknown", AgeUpperBoundMs = null }
            : frame).ToArray() };
    }
}
