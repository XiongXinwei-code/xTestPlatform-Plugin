using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Executors;

public sealed class CanTaskStatusExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var setting = (CanTaskStatusSetting)new CanTaskStatusPlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentException("状态查询总期限必须大于 0 ms");
            CanTaskStatusOutput.ValidateTarget(setting, context);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(setting.TimeoutMs);
            using var preparation = CanCyclicPreparationGate.Enter(context.Resources, "status/" + step.StepSetting.StepAddress, setting.TimeoutMs, deadline.Token);
            using var owned = PluginCommon.ExecutionScopedExecutor.OwnOperation(preparation.Dispose);
            long started = Stopwatch.GetTimestamp();
            void Check()
            {
                preparation.Check(); guard.Check();
                if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= setting.TimeoutMs) throw new TimeoutException("CAN 后台状态查询超过配置总期限");
            }
            string name = await preparation.Evaluations.EvaluateAsync<string>(setting.ConnectionName, context, preparation.Token, Check).ConfigureAwait(false);
            string taskName = await preparation.Evaluations.EvaluateAsync<string>(setting.TaskName, context, preparation.Token, Check).ConfigureAwait(false);
            if (!context.Resources.TryGet<CanConnectionResource>(CanHelper.GetAdapterKey(name), out var owner)) throw new InvalidOperationException($"CAN 连接 {name} 未找到");
            guard.Watch(CanHelper.GetAdapterKey(name), owner); Check();
            var send = owner.GetCyclicStatuses().Select(item =>
            {
                Check();
                var snapshot = JsonSerializer.SerializeToNode(item)!;
                // This step exposes hexadecimal IDs; keep the shared runtime snapshot numeric for its other consumers.
                if (snapshot["Messages"] is JsonArray rows)
                    foreach (var row in rows)
                    {
                        Check();
                        uint id = row!["Id"]!.GetValue<uint>();
                        row["Id"] = $"0x{id:X}";
                    }
                return snapshot;
            }).ToArray();
            var receive = owner.GetBackgroundReadStatuses().Select(item => { Check(); return JsonSerializer.SerializeToNode(item)!; }).ToArray();
            bool Matches(JsonNode item) => string.IsNullOrWhiteSpace(taskName) || item["TaskName"]?.GetValue<string>() == taskName;
            if (!string.IsNullOrWhiteSpace(taskName) && !send.Concat(receive).Any(Matches)) throw new InvalidOperationException($"CAN 后台任务 {taskName} 不存在");
            bool Include(JsonNode item) => Matches(item) && (setting.IncludeInactive || item["Active"]?.GetValue<bool>() == true);
            if (!setting.IncludeInactive)
                foreach (var item in send)
                    if (item["Messages"] is JsonArray messages)
                        item["Messages"] = new JsonArray(messages.Where(row => row?["Active"]?.GetValue<bool>() == true).Select(row => row!.DeepClone()).ToArray());
            string output = JsonSerializer.Serialize(new { ConnectionName = name, ConnectionEpoch = owner.Epoch, SendTasks = send.Where(Include), ReceiveTasks = receive.Where(Include) });
            Check(); CanTaskStatusOutput.ValidateTarget(setting, context); Check();
            guard.SetVariable(setting.ResultVariable, output); Check();
            return CanExecutionResults.Passed($"CAN 后台状态已写入 {setting.ResultVariable}（JSON）");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex)
        { return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"CAN 后台状态查询失败：{ex.Message}") } }; }
    }
}
