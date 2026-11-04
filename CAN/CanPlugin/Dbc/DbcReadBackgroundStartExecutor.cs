using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

internal sealed class DbcReadBackgroundStartExecutor : DbcExecutorBase<DbcReadBackgroundSetting>
{
    protected override IStepPlugin Plugin => new DbcReadBackgroundStartPlugin();

    protected override async Task<ExecutionResult> RunAsync(DbcReadBackgroundSetting setting, DbcExecution run)
    {
        if (setting.PublishIntervalMs <= 0 || setting.MaxAgeMs <= 0)
            throw new ArgumentException("DBC 后台读取的发布间隔和最新值有效期必须大于 0 ms");
        string name = await run.TextAsync(setting.TaskName).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("DBC 后台接收任务名不能为空");
        var (database, hash) = await run.LoadAsync(setting).ConfigureAwait(false);
        var selected = await run.SelectMessagesAsync(database, setting).ConfigureAwait(false);
        var mapping = new DbcReadBackgroundMapping(setting, selected, hash, run.Context);
        var owner = await run.ConnectionAsync(setting).ConfigureAwait(false);
        await run.PrepareReceiveAsync(owner).ConfigureAwait(false);
        var routes = selected.Select(item => new CanLatestRoute(item.Identity.Id, item.Identity.Type,
            item.Identity.Fd ? CanReceiveFrameFormat.Fd : CanReceiveFrameFormat.Classic, setting.MaxAgeMs)).ToArray();
        run.Check();
        int timeout = run.Remaining();
        var lease = CanBackgroundOutputLease.Acquire(run.Context, run.Guard, mapping.Destinations);
        // The owner adopts the lease on entry, including a failed/cancelled admission; callbacks retain it until actual exit.
        await owner.StartBackgroundReadAsync(name, routes, setting.PublishIntervalMs, timeout, run.Token, lease,
            async (snapshot, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var frames = snapshot.Frames.Select(item => new DbcBackgroundFrame(item.Route.Id, item.Route.FrameType,
                    item.Route.Format == CanReceiveFrameFormat.Fd, item.Frame, item.Sequence, item.Valid,
                    item.Quality, item.AgeUpperBoundMs)).ToArray();
                var pending = await mapping.PrepareAsync(frames, ct).ConfigureAwait(false);
                lease.Publish(pending, ct);
                mapping.Published(frames);
            }, "DBC").ConfigureAwait(false);
        return CanExecutionResults.Passed($"DBC 后台接收已启动：{name}，{selected.Length} 个 ID，最新值覆盖发布");
    }
}
