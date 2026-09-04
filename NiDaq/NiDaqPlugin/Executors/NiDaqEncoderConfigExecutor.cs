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
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqEncoderConfigPlugin().CreateSerializer();
        var setting = (NiDaqEncoderConfigSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var counterCh = await Evaluator.EvalStringAsync(setting.CounterChannel, context);

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");
            if (string.IsNullOrWhiteSpace(counterCh))
                return ErrorResult("Counter 通道不能为空");

            // 若已存在同名任务（序列异常终止未销毁），先销毁旧任务
            var existingTask = NiDaqTaskRegistry.Remove(taskName);
            if (existingTask is DaqTask oldTask)
            {
                try { oldTask.Dispose(); } catch { /* 忽略销毁异常 */ }
                context.LogAction?.Invoke($"NI DAQ 任务 '{taskName}' 检测到已有任务，已自动销毁旧任务");
            }

            var decodingType = setting.DecodingType switch
            {
                EncoderDecodingType.X1 => CIEncoderDecodingType.X1,
                EncoderDecodingType.X2 => CIEncoderDecodingType.X2,
                _ => CIEncoderDecodingType.X4
            };

            var task = new DaqTask(taskName);
            task.CIChannels.CreateAngularEncoderChannel(
                counterCh, "",
                decodingType,
                setting.ZIndexEnable, 0,
                CIEncoderZIndexPhase.AHighBHigh,
                setting.PulsesPerRevolution, 0,
                CIAngularEncoderUnits.Ticks);

            NiDaqTaskRegistry.Set(taskName, task);
            // 存储转换参数供 Read 使用
            NiDaqTaskRegistry.SetMetadata(taskName, "DistancePerPulse", setting.DistancePerPulse);
            NiDaqTaskRegistry.SetMetadata(taskName, "Unit", setting.Unit);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"编码器任务 '{taskName}' 已配置 ({counterCh}, {setting.DecodingType})"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
