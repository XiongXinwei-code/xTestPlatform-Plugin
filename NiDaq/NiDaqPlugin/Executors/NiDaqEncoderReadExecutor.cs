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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new NiDaqEncoderReadPlugin().CreateSerializer();
            var setting = (NiDaqEncoderSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            guard.Check();
            var resultVar = setting.ResultVariable;

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            var resource = NiDaqTaskRegistry.Get(context, taskName);
            guard.EnsureCurrent(taskName, resource);
            double value = await resource.RunAsync(() =>
            {
                resource.RequireStarted();
                var task = resource.Primary;
                if (task.CIChannels.Count != 1 || resource.IsSync) throw new InvalidOperationException("该任务不是单编码器任务");
                task.Stream.Timeout = setting.ReadTimeoutMs;
                resource.EnsureOperationActive();
                return new CounterSingleChannelReader(task.Stream).ReadSingleSampleDouble();
            }, setting.ReadTimeoutMs, "编码器读取", cancellationToken);
            guard.EnsureCurrent(taskName, resource);
            if (resource.Unit != EncoderUnit.Pulses) value *= resource.DistancePerPulse;

            DaqResultPublisher.Publish(context, resultVar, value, cancellationToken, resource, taskName);
            guard.EnsureCurrent(taskName, resource);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"{value:F4}"
                }
            };
        }
        catch (TimeoutException ex)
        {
            return ErrorResult(ex.Message, ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
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
