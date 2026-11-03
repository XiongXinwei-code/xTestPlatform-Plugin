using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Executors;

public sealed class CanCyclicSendStartExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var setting = (CanCyclicSendStartSetting)new CanCyclicSendStartPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.ControlRefreshMs <= 0 || setting.ExpressionTimeoutMs <= 0)
                throw new ArgumentException("控制刷新间隔与表达式求值期限必须大于 0 ms");
            int refreshMs = setting.ControlRefreshMs, expressionTimeoutMs = setting.ExpressionTimeoutMs;
            string preparationKey = string.IsNullOrWhiteSpace(step.StepSetting.StepAddress)
                ? setting.ConnectionName + "/" + setting.TaskName : step.StepSetting.StepAddress;
            using var preparation = CanCyclicPreparationGate.Enter(context.Resources, preparationKey, expressionTimeoutMs, cancellationToken);
            void CheckIdentity() { preparation.Check(); guard.Check(); }
            using var ownedPreparation = PluginCommon.ExecutionScopedExecutor.OwnOperation(preparation.Dispose);
            var connName = await preparation.Evaluations.EvaluateAsync<string>(setting.ConnectionName, context, preparation.Token, CheckIdentity).ConfigureAwait(false);
            guard.Check();
            if (!context.Resources.TryGet<CanConnectionResource>(CanHelper.GetAdapterKey(connName), out var connection))
            {
                guard.Check();
                throw new InvalidOperationException($"CAN 连接 {connName} 未找到，请使用新版 CAN_Open");
            }
            var taskName = await preparation.Evaluations.EvaluateAsync<string>(setting.TaskName, context, preparation.Token, CheckIdentity).ConfigureAwait(false);
            guard.Watch(CanHelper.GetAdapterKey(connName), connection);
            if (string.IsNullOrWhiteSpace(taskName)) throw new ArgumentException("周期任务名不能为空");
            if (setting.Messages == null || setting.Messages.Count > 128) throw new ArgumentException("周期报文最多 128 行");
            var messages = setting.Messages.Select(m => m?.Copy() ?? throw new ArgumentException("周期报文行不能为空"))
                .Where(m => m.Enabled || !string.IsNullOrWhiteSpace(m.EnabledExpression)).ToArray();
            if (messages.Length == 0) return CanExecutionResults.Passed("没有配置报文，发送池保持不变");
            connection.ValidateCyclicProtocol(messages.Any(m => m.UseFdFrame));
            var definitions = new List<CanCyclicDefinition>();
            foreach (var message in messages)
            {
                var initial = await ReadControl(message, preparation.Evaluations, preparation.Token, CheckIdentity).ConfigureAwait(false);
                using var tableDeadline = CancellationTokenSource.CreateLinkedTokenSource(preparation.Token);
                tableDeadline.CancelAfter(expressionTimeoutMs);
                try
                {
                    message.Protection = await CanPayloadProtectionExpressions.ResolveAsync(message.Protection, context,
                        tableDeadline.Token, CheckIdentity, guard.Log, preparation.Evaluations.Track).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (!preparation.Token.IsCancellationRequested)
                { throw new TimeoutException($"Data ID 表求值超过 {expressionTimeoutMs} ms", ex); }
                var initialFrame = await Build(message, preparation.Evaluations, preparation.Token, CheckIdentity).ConfigureAwait(false);
                CanHelper.ValidateTransmitFrame(initialFrame);
                CanPayloadProtection.Validate(message.Protection, initialFrame.Data.Length);
                var evaluations = new CanCyclicEvaluationScope(expressionTimeoutMs);
                int sampleMs = string.IsNullOrWhiteSpace(message.EnabledExpression) && string.IsNullOrWhiteSpace(message.CycleTimeMsExpression) ? 0 : refreshMs;
                definitions.Add(new(initialFrame.Id, initialFrame.FrameType, initialFrame.IsFd, initial, sampleMs, expressionTimeoutMs,
                    message.Protection, (ct, check) => Build(message, evaluations, ct, check),
                    (ct, check) => ReadControl(message, evaluations, ct, check), guard.CheckLifetime, evaluations.DrainAsync));
            }
            CheckIdentity();
            connection.UpdateCyclicPool(taskName, "CAN", definitions.ToArray(), CheckIdentity);
            guard.Check();
            return CanExecutionResults.Passed($"周期发送池已更新：{connName}/{taskName}；{messages.Length} 个 ID；重复 ID 下一节拍使用新配置，未发送初始化帧");

            async Task<CanCyclicControl> ReadControl(CyclicMessageItem message, CanCyclicEvaluationScope evaluations, CancellationToken ct, Action check)
            {
                bool enabled = string.IsNullOrWhiteSpace(message.EnabledExpression) ? message.Enabled
                    : await evaluations.EvaluateAsync<bool>(message.EnabledExpression, context, ct, check).ConfigureAwait(false);
                int period = string.IsNullOrWhiteSpace(message.CycleTimeMsExpression) ? message.CycleTimeMs
                    : await evaluations.EvaluateAsync<int>(message.CycleTimeMsExpression, context, ct, check).ConfigureAwait(false);
                var result = new CanCyclicControl(enabled, period); CanDynamicCyclicLoop.Validate(result); return result;
            }
            async Task<CanMessage> Build(CyclicMessageItem message, CanCyclicEvaluationScope evaluations, CancellationToken ct, Action check)
            {
                uint id = CanHelper.ParseCanId(await evaluations.EvaluateAsync<string>(message.CanId, context, ct, check).ConfigureAwait(false));
                byte[] data = CanHelper.ParseHexData(await evaluations.EvaluateAsync<string>(message.Data, context, ct, check).ConfigureAwait(false));
                check(); return new() { Id = id, Data = data, FrameType = message.FrameType, IsFd = message.UseFdFrame };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex)
        { return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"周期启动失败：{ex.Message}") } }; }
    }
}
