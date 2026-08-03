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
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqEncoderReadPlugin().CreateSerializer();
        var setting = (NiDaqEncoderSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var counterCh = await Evaluator.EvaluateAsync<string>(setting.CounterChannel, context) ?? setting.CounterChannel;
            var resultVar = await Evaluator.EvaluateAsync<string>(setting.ResultVariable, context) ?? setting.ResultVariable;

            var decodingType = setting.DecodingType switch
            {
                EncoderDecodingType.X1 => CIEncoderDecodingType.X1,
                EncoderDecodingType.X2 => CIEncoderDecodingType.X2,
                _ => CIEncoderDecodingType.X4
            };

            using var task = new DaqTask();
            task.CIChannels.CreateAngularEncoderChannel(
                counterCh, "",
                decodingType,
                setting.ZIndexEnable, 0,
                CIEncoderZIndexPhase.AHighBHigh,
                setting.PulsesPerRevolution, 0,
                CIAngularEncoderUnits.Ticks);

            var reader = new CounterSingleChannelReader(task.Stream);
            task.Start();
            double rawPulses = reader.ReadSingleSampleDouble();
            task.Stop();

            // 转换为物理量
            double result = rawPulses * setting.DistancePerPulse;

            context.SetVariable(resultVar, result);

            string unitStr = setting.Unit switch
            {
                EncoderUnit.Degrees => "°",
                EncoderUnit.Millimeters => "mm",
                _ => "pulses"
            };

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"{result:F4} {unitStr}",
                    Unit = unitStr
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
                    Error = new ErrorInfo { Message = $"编码器读取失败：{ex.Message}" }
                }
            };
        }
    }
}
