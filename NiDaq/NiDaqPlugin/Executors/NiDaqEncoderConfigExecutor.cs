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
            var taskName = await Evaluator.EvaluateAsync<string>(setting.TaskName, context) ?? setting.TaskName;
            var counterCh = await Evaluator.EvaluateAsync<string>(setting.CounterChannel, context) ?? setting.CounterChannel;

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");
            if (string.IsNullOrWhiteSpace(counterCh))
                return ErrorResult("Counter 通道不能为空");

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

            context.SetVariable(taskName, task);
            // 存储转换参数供 Read 使用
            context.SetVariable($"{taskName}_DistancePerPulse", setting.DistancePerPulse);
            context.SetVariable($"{taskName}_Unit", setting.Unit);

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
            return ErrorResult($"编码器配置失败：{ex.Message}");
        }
    }

    private static ExecutionResult ErrorResult(string message) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = new ErrorInfo { Message = message }
        }
    };
}
