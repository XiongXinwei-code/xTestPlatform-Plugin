using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqEncoderConfigExecutor : IStepExecutor
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
            var serializer = new NiDaqEncoderConfigPlugin().CreateSerializer();
            var setting = (NiDaqEncoderConfigSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var counterCh = await Evaluator.EvalStringAsync(setting.CounterChannel, context);
            guard.Check();
            if (!double.IsFinite(setting.DistancePerPulse) || setting.DistancePerPulse <= 0 || !Enum.IsDefined(setting.Unit)) throw new ArgumentException("编码器换算参数无效");
            var owner = await DaqTaskFactory.CreateAndRegisterAsync(context, taskName, resource =>
            {
                DaqTaskFactory.ConfigureEncoder(resource, resource.AddTask(), counterCh, "", setting.DecodingType, setting.ZIndexEnable, setting.PulsesPerRevolution);
                resource.Unit = setting.Unit; resource.DistancePerPulse = setting.DistancePerPulse;
            }, cancellationToken, physicalChannels: [counterCh], timeoutMs: setting.TimeoutMs, epoch: epoch);
            configuredOwner = owner;

            guard.EnsureCurrent(taskName, owner);
            epoch.ThrowIfExpired();
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"编码器任务 '{taskName}' 已配置 ({counterCh}, {setting.DecodingType})"
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
            return ErrorResult($"编码器配置失败：{ex.Message}", ex);
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
