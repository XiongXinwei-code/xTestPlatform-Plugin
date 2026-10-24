using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqTaskStartExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new NiDaqTaskStartPlugin().CreateSerializer();
            var setting = (NiDaqTaskStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            guard.Check();

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            var resource = NiDaqTaskRegistry.Get(context, taskName);
            guard.EnsureCurrent(taskName, resource);
            await resource.StartAsync(cancellationToken, setting.TimeoutMs);
            guard.EnsureCurrent(taskName, resource);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"任务 '{taskName}' 已启动"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return ErrorResult($"任务启动失败：{ex.Message}", ex);
        }
    }

    private static ExecutionResult ErrorResult(string message, Exception? ex = null) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = ex is null ? new ErrorInfo { Message = message } : ErrorInfo.FromException(ex, message)
        }
    };
}
