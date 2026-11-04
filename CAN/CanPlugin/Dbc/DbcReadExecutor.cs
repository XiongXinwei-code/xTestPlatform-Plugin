using CAN.Helpers;
using CAN.Models;
using PluginCommon;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

internal sealed class DbcReadExecutor : DbcExecutorBase<DbcReadSetting>
{
    protected override IStepPlugin Plugin => new DbcReadPlugin();
    protected override async Task<ExecutionResult> RunAsync(DbcReadSetting setting, DbcExecution run)
    {
        var (database, hash) = await run.LoadAsync(setting).ConfigureAwait(false);
        var selected = await run.SelectMessagesAsync(database, setting).ConfigureAwait(false);
        var outputs = new DbcOutputPlan(setting, selected, run);
        var messages = selected.ToDictionary(item => item.Identity);
        var remaining = messages.Keys.ToHashSet();
        var owner = await run.ConnectionAsync(setting).ConfigureAwait(false);
        await run.PrepareReceiveAsync(owner).ConfigureAwait(false);
        using var capture = owner.StartCapture(selected.Select(item => item.Message.Id).Distinct().ToArray(), selected.Length,
            run.Remaining(), f => remaining.Remove((f.Id, f.FrameType, f.IsFd)), firstOnly: selected.Length == 1);
        using var captureStop = ExecutionScopedExecutor.OwnOperation(capture.Dispose);
        await capture.Finished.WaitAsync(run.Token).ConfigureAwait(false);
        run.Check();
        var frames = capture.Complete();
        if (frames.Length != selected.Length) throw new TimeoutException($"等待 DBC 报文超时，已读取 {frames.Length}/{selected.Length} 个 ID");
        var decoded = frames.Select(f => { run.Check(); var item = messages[(f.Id, f.FrameType, f.IsFd)];
            var signals = item.Message.Decode(f.Data); outputs.Observe(item, signals);
            return DbcPayload.DecodeFrame(item.Message, f, item.Protection, item.FilterSignals(signals)); }).ToArray();
        if (setting.UseMessageList)
            await outputs.PublishAsync(new { fingerprint = hash, count = frames.Length, frames = decoded }).ConfigureAwait(false);
        else await outputs.PublishAsync(new { fingerprint = hash, frame = decoded[0] }).ConfigureAwait(false);
        return CAN.Runtime.CanExecutionResults.Passed($"DBC 读取完成：{frames.Length} 个 ID，各一帧");
    }
}
