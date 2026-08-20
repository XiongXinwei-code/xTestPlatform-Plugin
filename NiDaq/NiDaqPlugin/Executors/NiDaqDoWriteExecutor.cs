using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqDoWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqDoWritePlugin().CreateSerializer();
        var setting = (NiDaqDoWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var channel = await Evaluator.EvalStringAsync(setting.Channel, context);
            var valueStr = await Evaluator.EvalStringAsync(setting.Value, context);

            using var task = new DaqTask();
            task.DOChannels.CreateChannel(channel, "", ChannelLineGrouping.OneChannelForAllLines);

            var writer = new DigitalSingleChannelWriter(task.Stream);

            if (bool.TryParse(valueStr, out bool boolVal))
            {
                await NiDaqTimeoutHelper.RunWithTimeoutAsync(
                    () => writer.WriteSingleSampleSingleLine(true, boolVal), NiDaqTimeoutHelper.DefaultTimeoutMs, "DO 输出", cancellationToken);
            }
            else if (uint.TryParse(valueStr, out uint uintVal))
            {
                await NiDaqTimeoutHelper.RunWithTimeoutAsync(
                    () => writer.WriteSingleSamplePort(true, uintVal), NiDaqTimeoutHelper.DefaultTimeoutMs, "DO 输出", cancellationToken);
            }
            else
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"无法解析输出值：{valueStr}，需为 bool 或整数" }
                    }
                };
            }

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = valueStr
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
                    Error = ErrorInfo.FromException(ex, $"DO 输出失败：{ex.Message}")
                }
            };
        }
    }
}
