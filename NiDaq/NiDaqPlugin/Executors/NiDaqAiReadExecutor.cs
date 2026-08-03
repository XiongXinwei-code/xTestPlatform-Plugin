using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqAiReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqAiReadPlugin().CreateSerializer();
        var setting = (NiDaqAiReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var taskName = await Evaluator.EvaluateAsync<string>(setting.TaskName, context) ?? setting.TaskName;
            var prefix = await Evaluator.EvaluateAsync<string>(setting.ResultVariablePrefix, context) ?? setting.ResultVariablePrefix;

            if (string.IsNullOrWhiteSpace(taskName))
                return ErrorResult("任务名称不能为空");

            var taskObj = context.GetVariable(taskName);
            if (taskObj is not DaqTask task)
                return ErrorResult($"未找到任务 '{taskName}'");

            var reader = new AnalogMultiChannelReader(task.Stream);
            int samplesToRead = setting.SamplesToRead > 0 ? setting.SamplesToRead : task.Stream.AvailableSamplesPerChannel > 0 ? (int)task.Stream.AvailableSamplesPerChannel : 100;
            double[,] data = reader.ReadMultiSample(samplesToRead);

            int channels = data.GetLength(0);
            int samples = data.GetLength(1);

            for (int ch = 0; ch < channels; ch++)
            {
                var name = task.AIChannels[ch].VirtualName;
                double sum = 0, max = double.MinValue, min = double.MaxValue;
                for (int s = 0; s < samples; s++)
                {
                    double v = data[ch, s];
                    sum += v;
                    if (v > max) max = v;
                    if (v < min) min = v;
                }
                double avg = sum / samples;

                context.SetVariable($"{prefix}_{name}_Avg", avg);
                context.SetVariable($"{prefix}_{name}_Max", max);
                context.SetVariable($"{prefix}_{name}_Min", min);
                context.SetVariable($"{prefix}_{name}_Count", samples);
            }

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"{channels} ch × {samples} samples"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult($"AI 读取失败：{ex.Message}");
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
