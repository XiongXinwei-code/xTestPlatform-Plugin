using System.Diagnostics;
using System.Text.Json;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Executors;

public sealed class CanReadBackgroundStartExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var setting = (CanReadBackgroundSetting)new CanReadBackgroundStartPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0 || setting.PublishIntervalMs <= 0 || setting.MaxAgeMs <= 0)
                throw new ArgumentException("启动期限、发布间隔与新鲜度期限必须大于 0");
            long started = Stopwatch.GetTimestamp();
            int Remaining() => setting.TimeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds) is int value && value > 0
                ? value : throw new TimeoutException($"CAN 监听启动超过总期限 {setting.TimeoutMs} ms");
            using var preparation = CanCyclicPreparationGate.Enter(context.Resources, "Listen:" + (step.StepSetting.StepAddress ?? setting.ConnectionName + "/" + setting.TaskName), setting.TimeoutMs, cancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(preparation.Token);
            deadline.CancelAfter(setting.TimeoutMs);
            void Check() { guard.Check(); preparation.Check(); _ = Remaining(); }
            async Task<string> Text(string expression) => await preparation.Evaluations.EvaluateAsync<string>(expression, context, deadline.Token, Check).ConfigureAwait(false);
            string connectionName = await Text(setting.ConnectionName).ConfigureAwait(false);
            string taskName = await Text(setting.TaskName).ConfigureAwait(false);
            string key = CanHelper.GetAdapterKey(connectionName);
            if (!context.Resources.TryGet<CanConnectionResource>(key, out var owner)) throw new InvalidOperationException($"CAN 连接 {connectionName} 不存在");
            guard.Watch(key, owner);
            var mappings = setting.Mappings?.Where(m => m != null && m.Enabled).ToArray() ?? [];
            if (mappings.Length is < 1 or > 128) throw new ArgumentException("需要 1~128 条启用的监听路由");
            var routes = new List<CanLatestRoute>();
            var paths = new List<string>();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable)) { DbcVariableBinding.ValidateJsonTarget(setting.ResultVariable, context); paths.Add(setting.ResultVariable); }
            foreach (var mapping in mappings)
            {
                uint id = CanHelper.ParseCanId(await Text(mapping.CanId).ConfigureAwait(false));
                if (!Enum.IsDefined(mapping.FrameType) || !Enum.IsDefined(mapping.FrameFormat) || id > (mapping.FrameType == CanFrameType.Standard ? 0x7FFu : 0x1FFFFFFFu))
                    throw new ArgumentException("监听 ID、帧类型或格式无效");
                if (routes.Any(r => r.Id == id && r.FrameType == mapping.FrameType &&
                    (r.Format == mapping.FrameFormat || r.Format == CanReceiveFrameFormat.Any || mapping.FrameFormat == CanReceiveFrameFormat.Any)))
                    throw new ArgumentException($"监听 ID 0x{id:X} 的格式筛选重叠");
                routes.Add(new(id, mapping.FrameType, mapping.FrameFormat, setting.MaxAgeMs));
                if (string.IsNullOrWhiteSpace(mapping.DataVariable)) continue;
                DbcVariableBinding.ValidateJsonTarget(mapping.DataVariable, context); paths.Add(mapping.DataVariable);
            }
            Check();
            var outputs = CanBackgroundOutputLease.Acquire(context, guard, paths);
            var sequences = new long[mappings.Length];
            var dataPaths = mappings.Select(m => m.DataVariable).ToArray();
            string jsonPath = setting.ResultVariable;
            // 所有权在进入 StartBackgroundReadAsync 时转移；失败也由 runtime 等真实退出后释放。
            int remaining;
            try { remaining = Remaining(); }
            catch { outputs.Dispose(); throw; }
            await owner.StartBackgroundReadAsync(taskName, routes.ToArray(), setting.PublishIntervalMs, remaining,
                cancellationToken, outputs, (snapshot, ct) =>
                {
                    var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < snapshot.Frames.Length; i++)
                    {
                        var item = snapshot.Frames[i];
                        if (item.Frame != null && item.Sequence != sequences[i] && item.Quality is not ("Stale" or "OutOfOrder" or "ClockReset") && !string.IsNullOrWhiteSpace(dataPaths[i]))
                        {
                            DbcVariableBinding.ValidateJsonTarget(dataPaths[i], context);
                            values.Add(dataPaths[i], BitConverter.ToString(item.Frame.Data).Replace('-', ' '));
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(jsonPath))
                    {
                        DbcVariableBinding.ValidateJsonTarget(jsonPath, context);
                        values.Add(jsonPath, JsonSerializer.Serialize(new
                    {
                        taskName, snapshot.Running, snapshot.ReceivedFrames, snapshot.Replacements,
                        frames = snapshot.Frames.Select(f => new { id = $"0x{f.Route.Id:X}", frameType = f.Route.FrameType.ToString(),
                            isFd = f.Frame?.IsFd, data = f.Frame == null ? null : BitConverter.ToString(f.Frame.Data).Replace('-', ' '),
                            f.Sequence, f.Valid, f.Quality, f.AgeUpperBoundMs, f.Frame?.TimestampNs, f.Frame?.TimestampDomain,
                            f.Frame?.ClockId, f.Frame?.RawTimestamp100ns, f.Frame?.RawTimestampUs, f.Frame?.CaptureMonotonicLowerBoundTicks })
                    }));
                    }
                    outputs.Publish(values, ct);
                    for (int i = 0; i < snapshot.Frames.Length; i++) sequences[i] = snapshot.Frames[i].Sequence;
                    return Task.CompletedTask;
                }, "Raw").ConfigureAwait(false);
            return CanExecutionResults.Passed($"总线监听已启动：{connectionName}/{taskName}；{routes.Count} 个 ID，最新值覆盖；尚未收到不代表失败");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return ExecutionResults.Aborted(); }
        catch (Exception ex) { return ExecutionResults.FromException(ex, $"启动 CAN 总线监听失败：{ex.Message}"); }
    }
}
