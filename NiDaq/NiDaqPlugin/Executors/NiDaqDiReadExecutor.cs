using NationalInstruments.DAQmx;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.Executors;

public sealed class NiDaqDiReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqDiReadPlugin().CreateSerializer();
        var setting = (NiDaqDiReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var channel = await Evaluator.EvaluateAsync<string>(setting.Channel, context) ?? setting.Channel;
            var resultVar = await Evaluator.EvaluateAsync<string>(setting.ResultVariable, context) ?? setting.ResultVariable;

            using var task = new NationalInstruments.DAQmx.Task();
            task.DIChannels.CreateChannel(channel, "", ChannelLineGrouping.OneChannelForAllLines);

            var reader = new DigitalSingleChannelReader(task.Stream);
            uint data = reader.ReadSingleSamplePortUInt32();

            context.SetVariable(resultVar, data);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = data.ToString()
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"DI 读取失败：{ex.Message}" }
                }
            };
        }
    }
}
