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
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqTaskStartPlugin().CreateSerializer();
        var setting = (NiDaqTaskStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            var taskObj = context.GetVariable(taskName);
            if (taskObj is not DaqTask task)
                return ErrorResult($"未找到已配置的任务 '{taskName}'，请先执行对应的 Config 步骤");

            task.Start();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"任务 '{taskName}' 已启动"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
