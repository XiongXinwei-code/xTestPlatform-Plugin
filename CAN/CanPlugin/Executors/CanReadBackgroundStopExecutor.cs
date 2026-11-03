using System.Diagnostics;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Executors;

public sealed class CanReadBackgroundStopExecutor(IStepPlugin? serializerPlugin = null) : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var setting = (CanReadBackgroundStopSetting)(serializerPlugin ?? new CanReadBackgroundStopPlugin()).CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentException("停止总期限必须大于 0");
            var clock = Stopwatch.StartNew();
            int Remaining() => setting.TimeoutMs > clock.ElapsedMilliseconds ? setting.TimeoutMs - (int)clock.ElapsedMilliseconds
                : throw new TimeoutException($"CAN 停止监听超过总期限 {setting.TimeoutMs} ms");
            using var preparation = CanCyclicPreparationGate.Enter(context.Resources, "StopListen:" + (step.StepSetting.StepAddress ?? setting.ConnectionName + "/" + setting.TaskName), setting.TimeoutMs, cancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(preparation.Token);
            deadline.CancelAfter(setting.TimeoutMs);
            void Check() { guard.Check(); preparation.Check(); _ = Remaining(); }
            async Task<string> Text(string expression) => await preparation.Evaluations.EvaluateAsync<string>(expression, context, deadline.Token, Check).ConfigureAwait(false);
            var connectionName = await Text(setting.ConnectionName).ConfigureAwait(false);
            var taskName = await Text(setting.TaskName).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(taskName)) throw new ArgumentException("监听任务名不能为空");
            if (!context.Resources.TryGet<CanConnectionResource>(CanHelper.GetAdapterKey(connectionName), out var owner))
            {
                Check();
                if (setting.IgnoreIfMissing) return CanExecutionResults.Passed("CAN 连接不存在，无监听需要停止");
                throw new InvalidOperationException($"CAN 连接 {connectionName} 不存在");
            }
            guard.Watch(CanHelper.GetAdapterKey(connectionName), owner);
            var warning = await owner.StopBackgroundReadAsync(taskName, Remaining(), cancellationToken, setting.IgnoreIfMissing).ConfigureAwait(false);
            Check();
            if (warning != null) guard.Log(LogLevel.Warn, $"监听 {taskName} 已退出；保留历史后台故障", warning);
            return CanExecutionResults.Passed($"监听已停止：{connectionName}/{taskName}；发布任务真实退出，变量保留最后值");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return ExecutionResults.Aborted(); }
        catch (Exception ex) { return ExecutionResults.FromException(ex, $"停止 CAN 总线监听失败：{ex.Message}"); }
    }
}
