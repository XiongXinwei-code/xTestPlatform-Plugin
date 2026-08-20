using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

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
            NiDriverCheck.EnsureDriver();
            var channel = await Evaluator.EvalStringAsync(setting.Channel, context);
            var resultVar = setting.ResultVariable;

            using var task = new DaqTask();
            task.DIChannels.CreateChannel(channel, "", ChannelLineGrouping.OneChannelForAllLines);

            var reader = new DigitalSingleChannelReader(task.Stream);
            uint data = await NiDaqTimeoutHelper.RunWithTimeoutAsync(
                () => reader.ReadSingleSamplePortUInt32(), NiDaqTimeoutHelper.DefaultTimeoutMs, "DI 读取", cancellationToken);

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
        catch (TimeoutException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex)
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
                    Error = ErrorInfo.FromException(ex, $"DI 读取失败：{ex.Message}")
                }
            };
        }
    }
}
