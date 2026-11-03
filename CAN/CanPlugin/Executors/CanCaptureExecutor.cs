using System.Text.Json;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using PluginCommon;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanCaptureExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        string? exportedFile = null;
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var s = (CanCaptureSetting)new CanCapturePlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (s.DurationMs <= 0 || s.SetupTimeoutMs <= 0 || s.MaxFrames is < 1 or > 100_000)
                throw new ArgumentException("采集时间和接收准备超时必须大于 0，最大帧数须为 1~100000");
            if (s.ExportEnabled && s.ExportTimeoutMs <= 0) throw new ArgumentException("导出超时必须大于 0");
            if (string.IsNullOrWhiteSpace(s.ResultVariable) || s.ResultVariable.IndexOf('.') <= 0 || !context.HasVariable(s.ResultVariable) ||
                context.GetVariable(s.ResultVariable) is { } current && current is not string)
                throw new ArgumentException("结果变量必须是已存在的字符串变量");
            var name = await Evaluator.EvalStringAsync(s.ConnectionName, context);
            var key = CanHelper.GetAdapterKey(name);
            if (!context.Resources.TryGet<CanConnectionResource>(key, out var owner))
                throw new InvalidOperationException($"CAN 连接 {name} 未找到，请先打开连接");
            guard.Watch(key, owner);
            var filter = await Evaluator.EvalStringAsync(s.FilterIds, context);
            var ids = filter.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(CanHelper.ParseCanId).Distinct().ToArray();
            if (ids.Any(id => id > 0x1FFFFFFF)) throw new ArgumentException("过滤 ID 超过 29 位范围");
            var exportPath = s.ExportEnabled
                ? CanCaptureFileExport.NormalizePath(await Evaluator.EvalStringAsync(s.ExportFilePath, context)) : null;
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var operation = ExecutionScopedExecutor.OwnOperation(() => _ = stop.CancelAsync());
            await guard.RunAsync(() => Task.Run(() => { guard.Check(); owner.EnsureSharedReceive(s.SetupTimeoutMs, stop.Token); guard.Check(); }, CancellationToken.None));
            guard.Check();
            using var capture = owner.StartCapture(ids, s.MaxFrames, s.DurationMs);
            using var period = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            var delay = Task.Delay(s.DurationMs, period.Token);
            await Task.WhenAny(delay, capture.Finished).ConfigureAwait(false);
            await period.CancelAsync().ConfigureAwait(false);
            guard.Check(); stop.Token.ThrowIfCancellationRequested();
            var frames = capture.Complete();
            bool isFull = capture.IsFull;
            var json = JsonSerializer.Serialize(new
            {
                durationMs = s.DurationMs, filterIds = ids.Select(id => $"0x{id:X}").ToArray(),
                maximumFrames = s.MaxFrames, isFull,
                count = frames.Length, frames = frames.Select(f => new {
                    id = $"0x{f.Id:X}", frameType = f.FrameType.ToString(), isFd = f.IsFd,
                    length = f.Data.Length, data = Convert.ToHexString(f.Data), f.TimestampNs,
                    f.RawTimestamp100ns, f.RawTimestampUs, f.TimestampDomain, f.ClockId,
                    f.IngressMonotonicTicks, f.CaptureMonotonicLowerBoundTicks }).ToArray()
            });
            if (exportPath != null)
            {
                await CanCaptureFileExport.Default.ExportAsync(exportPath, json, s.OverwriteExportFile,
                    s.ExportTimeoutMs, stop.Token, guard.Check).ConfigureAwait(false);
                exportedFile = exportPath;
            }
            guard.SetVariable(s.ResultVariable, json);
            return CAN.Runtime.CanExecutionResults.Passed($"CAN 采集完成：{frames.Length} 帧 / 最长窗口 {s.DurationMs} ms" +
                (isFull ? $"；已满（上限 {s.MaxFrames} 帧，后续数据不再采集）" : "") +
                (exportedFile == null ? "" : $"；已导出：{exportedFile}"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return ExecutionResults.Aborted(); }
        catch (Exception ex) { return ExecutionResults.FromException(ex, $"CAN 采集失败：{ex.Message}" +
            (exportedFile == null ? "" : $"；文件已导出，但后续结果发布失败：{exportedFile}")); }
    }
}
