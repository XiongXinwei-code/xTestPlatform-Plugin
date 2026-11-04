using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using System.Diagnostics;
using System.Text.Json;
using PluginCommon;

namespace CAN.Executors;

public sealed class CanReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new CanReadPlugin().CreateSerializer();
            var setting = (CanReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (!Enum.IsDefined(setting.ReadMode) || setting.ReadTimeoutMs == 0 || setting.ReadTimeoutMs < -1)
                throw new ArgumentException("读取模式无效，或读取期限不是正数/-1");
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = CanHelper.GetAdapterKey(connName);
            if (!context.Resources.TryGet<ICanAdapter>(key, out var adapter))
            {
                guard.Check();
                return ExecutionResults.Error($"CAN 连接未找到: {connName}");
            }

            // 解析过滤 ID
            guard.Watch(key, adapter);
            var ids = CanReadFilter.ParseIds(string.IsNullOrWhiteSpace(setting.FilterId) ? "" :
                await Evaluator.EvalStringAsync(setting.FilterId, context));
            if (setting.ReadMode == CanReadMode.EachId && ids.Length == 0)
                throw new ArgumentException("每 ID 一帧模式必须指定至少一个 ID");
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable) &&
                string.Equals(setting.ResultVariable, setting.IdVariable, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("数据结果变量和 ID 变量不能使用同一路径");

            // 读取
            if (adapter is not CAN.Runtime.CanConnectionResource owner)
                throw new InvalidOperationException("旧 CAN 连接没有实际 I/O 生命周期所有者，请关闭后重新执行 CAN_Open");
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var operation = ExecutionScopedExecutor.OwnOperation(() => _ = stop.CancelAsync());
            var frames = new List<CanMessage>();
            var remainingIds = ids.ToHashSet();
            long started = Stopwatch.GetTimestamp();
            do
            {
                int remaining = setting.ReadTimeoutMs == -1 ? -1 : setting.ReadTimeoutMs -
                    (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                CanMessage? frame = null;
                if (setting.ReadTimeoutMs == -1 || remaining > 0)
                    frame = await guard.RunAsync(() => ids.Length > 1 || setting.ReadMode == CanReadMode.EachId
                        ? owner.ReadAnyAsync(remainingIds.ToArray(), remaining, stop.Token)
                        : owner.ReadAsync(ids.Length == 0 ? null : ids[0], remaining, stop.Token));
                if (frame == null)
                    return ExecutionResults.Error($"CAN 连接 {connName} 读取超时 ({setting.ReadTimeoutMs}ms)",
                        detail: $"已读 {frames.Count} 帧；缺失/筛选 ID={string.Join(",", remainingIds.Select(id => $"0x{id:X}"))}；未发布部分结果");
                frames.Add(frame); remainingIds.Remove(frame.Id);
            }
            while (setting.ReadMode == CanReadMode.EachId && remainingIds.Count > 0);

            stop.Token.ThrowIfCancellationRequested(); guard.Check();
            var msg = frames[0];
            var dataHex = BitConverter.ToString(msg.Data).Replace("-", " ");
            string result = setting.ReadMode == CanReadMode.FirstMatching ? dataHex : JsonSerializer.Serialize(new
            {
                count = frames.Count,
                frames = frames.Select(f => new { id = $"0x{f.Id:X}", frameType = f.FrameType.ToString(), isFd = f.IsFd,
                    data = BitConverter.ToString(f.Data).Replace("-", " "), length = f.Data.Length,
                    f.TimestampNs, f.TimestampDomain, f.ClockId }).ToArray()
            });

            // 存储结果
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                guard.SetVariable(setting.ResultVariable, result);
            if (!string.IsNullOrWhiteSpace(setting.IdVariable))
                guard.SetVariable(setting.IdVariable, string.Join(",", frames.Select(f => $"0x{f.Id:X}")));

            guard.Log($"CAN 接收: {frames.Count} 帧，ID=[{string.Join(",", frames.Select(f => $"0x{f.Id:X}"))}]");

            return CAN.Runtime.CanExecutionResults.Passed(setting.ReadMode == CanReadMode.FirstMatching
                ? $"0x{msg.Id:X}: {dataHex}" : $"CAN 读取完成：{frames.Count} 个 ID，各一帧，结果为带 ID 的 JSON");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ExecutionResults.Aborted();
        }
        catch (Exception ex)
        {
            return ExecutionResults.FromException(ex, $"CAN 读取失败: {ex.Message}");
        }
    }

}
