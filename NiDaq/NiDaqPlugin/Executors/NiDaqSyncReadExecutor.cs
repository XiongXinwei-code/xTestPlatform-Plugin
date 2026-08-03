using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqSyncReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqSyncReadPlugin().CreateSerializer();
        var setting = (NiDaqSyncReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

            // 读取 AI 数据
            var aiReader = new AnalogMultiChannelReader(task.Stream);
            int samplesToRead = setting.SamplesToRead > 0 ? setting.SamplesToRead : 100;
            double[,] aiData = aiReader.ReadMultiSample(samplesToRead);

            int aiChannels = aiData.GetLength(0);
            int samples = aiData.GetLength(1);

            for (int ch = 0; ch < aiChannels; ch++)
            {
                var name = task.AIChannels[ch].VirtualName;
                double sum = 0, max = double.MinValue, min = double.MaxValue;
                for (int s = 0; s < samples; s++)
                {
                    double v = aiData[ch, s];
                    sum += v;
                    if (v > max) max = v;
                    if (v < min) min = v;
                }
                context.SetVariable($"{prefix}_{name}_Avg", sum / samples);
                context.SetVariable($"{prefix}_{name}_Max", max);
                context.SetVariable($"{prefix}_{name}_Min", min);
            }

            // 读取编码器数据
            var ciReader = new CounterSingleChannelReader(task.Stream);
            double encoderValue = ciReader.ReadSingleSampleDouble();
            context.SetVariable($"{prefix}_Encoder", encoderValue);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"Sync: {aiChannels} AI ch × {samples} samples + encoder"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult($"同步读取失败：{ex.Message}");
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
