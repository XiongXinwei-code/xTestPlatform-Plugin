using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqAiConfigExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        DaqTaskResource? configuredOwner = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            var epoch = guard.Epoch;
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new NiDaqAiConfigPlugin().CreateSerializer();
            var setting = (NiDaqAiConfigSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            guard.Check();
            var owner = await DaqTaskFactory.CreateAndRegisterAsync(context, taskName, resource =>
            {
                var task = resource.AddTask();
                DaqTaskFactory.ConfigureAi(resource, task, setting.Channels);
                DaqTaskFactory.ConfigureClock(resource, task, setting.ClockSource, setting.SampleRate, setting.SampleMode, setting.SamplesPerChannel);
                DaqTaskFactory.ConfigureTrigger(resource, task, setting.UseTrigger, setting.TriggerSource, setting.TriggerEdge);
            }, cancellationToken, physicalChannels: setting.Channels.Select(c => c.PhysicalChannel), timeoutMs: setting.TimeoutMs, epoch: epoch);
            configuredOwner = owner;

            guard.EnsureCurrent(taskName, owner);
            epoch.ThrowIfExpired();
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"AI 任务 '{taskName}' 已配置 ({setting.Channels.Count} 通道, {setting.SampleRate} Hz)"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            configuredOwner?.Dispose();
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            configuredOwner?.Dispose();
            return ErrorResult($"AI 配置失败：{ex.Message}", ex);
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
