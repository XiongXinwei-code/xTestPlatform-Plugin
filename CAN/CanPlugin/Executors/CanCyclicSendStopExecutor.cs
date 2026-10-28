using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanCyclicSendStopExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var setting = (CanCyclicSendStopSetting)new CanCyclicSendStopPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "停止总超时必须大于 0");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int Remaining()
            {
                guard.Check();
                return setting.TimeoutMs > clock.ElapsedMilliseconds ? setting.TimeoutMs - (int)clock.ElapsedMilliseconds
                    : throw new TimeoutException($"CAN 周期停止超过总期限 {setting.TimeoutMs} ms");
            }
            var evaluator = ExpressionEvaluatorFactory.Default;
            var name = await evaluator.EvalStringAsync(setting.ConnectionName, context);
            var taskName = await evaluator.EvalStringAsync(setting.TaskName, context);
            if (!context.Resources.TryGet<CanConnectionResource>(CanHelper.GetAdapterKey(name), out var connection))
            {
                guard.Check();
                throw new InvalidOperationException($"CAN 连接 {name} 未找到");
            }
            guard.Watch(CanHelper.GetAdapterKey(name), connection);
            var warning = await guard.RunAsync(() => connection.StopJobWithDiagnosticsAsync(taskName, Remaining(), cancellationToken));
            if (warning != null)
                guard.Log(LogLevel.Warn, $"CAN 周期任务 {name}/{taskName} 的实际写调用已退出；保留历史诊断", warning);
            _ = Remaining();
            return new() { StepResult = new() { Status = TestStatus.Completed, Value = $"周期工作任务已退出：{name}/{taskName}；不代表电机停止" } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex)
        { return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"周期停止失败：{ex.Message}") } }; }
    }
}
