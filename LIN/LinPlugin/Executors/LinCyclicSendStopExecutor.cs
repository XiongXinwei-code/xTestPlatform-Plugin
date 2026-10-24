using LIN.Runtime;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinCyclicSendStopExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new LIN.Runtime.LinExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new LinCyclicSendStopPlugin().CreateSerializer();
            var setting = (LinCyclicSendStopSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int Remaining() => setting.TimeoutMs > clock.ElapsedMilliseconds ? setting.TimeoutMs - (int)clock.ElapsedMilliseconds : throw new TimeoutException($"LIN 周期停止超过总期限 {setting.TimeoutMs} ms");
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var taskKey = LinCyclicSendStartExecutor.GetTaskKey(taskName);
            guard.Check();

            context.Resources.TryGet<LinCyclicJob>(taskKey, out var expected);
            guard.Check();
            expected?.Cancel();
            using var registryLease = await LinRegistryGuard.Enter(context.Resources, cancellationToken, Remaining());
            guard.Check();
            if (context.Resources.TryGet<LinCyclicJob>(taskKey, out var cts) && ReferenceEquals(cts, expected))
            {
                await cts.StopAsync(Remaining(), cancellationToken);
                if (context.Resources.TryGet<LinCyclicJob>(taskKey, out var current) && ReferenceEquals(current, cts)) context.Resources.Remove(taskKey);
                guard.Log($"LIN 周期发送已停止: TaskName={taskName}");

                guard.Check();

                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Completed,
                        Value = $"已停止周期发送任务: {taskName}"
                    }
                };
            }

            guard.Check();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"未找到运行中的周期发送任务: {taskName}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"LIN 周期发送停止失败: {ex.Message}")
                }
            };
        }
    }
}
