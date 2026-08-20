using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqEncoderReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqEncoderReadPlugin().CreateSerializer();
        var setting = (NiDaqEncoderSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var resultVar = setting.ResultVariable;

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            var taskObj = context.GetVariable(taskName);
            if (taskObj is not DaqTask task)
                return ErrorResult($"未找到任务 '{taskName}'");

            var reader = new CounterSingleChannelReader(task.Stream);
            task.Stream.Timeout = setting.ReadTimeoutMs > 0 ? setting.ReadTimeoutMs : -1;
            double value = await NiDaqTimeoutHelper.RunWithTimeoutAsync(
                () => reader.ReadSingleSampleDouble(), setting.ReadTimeoutMs, "编码器读取", cancellationToken);

            context.SetVariable(resultVar, value);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"{value:F4}"
                }
            };
        }
        catch (TimeoutException ex)
        {
            return ErrorResult(ex.Message, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult($"编码器读取失败：{ex.Message}", ex);
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
