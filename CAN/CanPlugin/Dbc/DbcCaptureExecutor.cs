using CAN.Helpers;
using CAN.Models;
using PluginCommon;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

internal sealed class DbcCaptureExecutor : DbcExecutorBase<DbcCaptureSetting>
{
    protected override IStepPlugin Plugin => new DbcCapturePlugin();
    protected override async Task<ExecutionResult> RunAsync(DbcCaptureSetting setting, DbcExecution run)
    {
        if (setting.DurationMs <= 0 || setting.MaxFrames is < 1 or > 100_000) throw new ArgumentException("采集时间必须大于 0 ms，帧数上限必须为 1～100000");
        var (database, hash) = await run.LoadAsync(setting).ConfigureAwait(false);
        var selected = await run.SelectMessagesAsync(database, setting).ConfigureAwait(false);
        var outputs = new DbcOutputPlan(setting, selected, run);
        var messages = selected.ToDictionary(item => item.Identity);
        var owner = await run.ConnectionAsync(setting).ConfigureAwait(false);
        await run.PrepareReceiveAsync(owner).ConfigureAwait(false);
        run.BeginCapturePeriod();
        using var capture = owner.StartCapture(messages.Keys.Select(k => k.Id).Distinct().ToArray(), setting.MaxFrames, setting.DurationMs,
            f => messages.ContainsKey((f.Id, f.FrameType, f.IsFd)));
        using var captureStop = ExecutionScopedExecutor.OwnOperation(capture.Dispose);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(run.Token);
        try
        {
            await Task.WhenAny(Task.Delay(setting.DurationMs, wait.Token), capture.Finished).ConfigureAwait(false);
            run.Check();
            var frames = capture.Complete();
            long totalSignals = 0;
            var decoded = new object[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                run.Check(); var frame = frames[i];
                var item = messages[(frame.Id, frame.FrameType, frame.IsFd)];
                totalSignals += item.Message.Signals.Count;
                if (totalSignals > 1_000_000) throw new InvalidOperationException("DBC 采集结果超过 1000000 个信号上限，请缩短采集时长或减少目标报文");
                var signals = item.Message.Decode(frame.Data); outputs.Observe(item, signals);
                decoded[i] = DbcPayload.DecodeFrame(item.Message, frame, item.Protection, item.FilterSignals(signals));
            }
            await outputs.PublishAsync(new { fingerprint = hash, durationMs = setting.DurationMs,
                maximumFrames = setting.MaxFrames, isFull = capture.IsFull, count = frames.Length, frames = decoded }).ConfigureAwait(false);
            return CAN.Runtime.CanExecutionResults.Passed($"DBC 采集完成：{frames.Length} 帧 / 配置 {setting.DurationMs} ms" +
                (capture.IsFull ? "；已满，后续数据不再采集" : ""));
        }
        finally { await wait.CancelAsync().ConfigureAwait(false); }
    }
}
