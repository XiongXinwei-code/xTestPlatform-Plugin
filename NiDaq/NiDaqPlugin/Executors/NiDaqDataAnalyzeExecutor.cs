using NationalInstruments.Tdms;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.Executors;

public sealed class NiDaqDataAnalyzeExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new NiDaqDataAnalyzePlugin().CreateSerializer();
        var setting = (NiDaqDataAnalyzeSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            NiDriverCheck.EnsureDriver();
            var filePath = await Evaluator.EvaluateAsync<string>(setting.FilePath, context) ?? setting.FilePath;
            var channelName = await Evaluator.EvaluateAsync<string>(setting.ChannelName, context) ?? setting.ChannelName;
            var resultVar = await Evaluator.EvaluateAsync<string>(setting.ResultVariable, context) ?? setting.ResultVariable;
            var refChannel = await Evaluator.EvaluateAsync<string>(setting.ReferenceChannel, context) ?? setting.ReferenceChannel;
            var refAtPeakVar = await Evaluator.EvaluateAsync<string>(setting.RefAtPeakVariable, context) ?? setting.RefAtPeakVariable;

            if (!File.Exists(filePath))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"数据文件不存在：{filePath}" }
                    }
                };
            }

            using var tdmsFile = new TdmsFile(filePath, TdmsFileAccess.Read);
            tdmsFile.Open();
            var groups = tdmsFile.GetChannelGroups();
            TdmsChannel? mainCh = null;
            TdmsChannel? refCh = null;

            foreach (TdmsChannelGroup group in groups)
            {
                var channels = group.GetChannels();
                mainCh ??= channels.Cast<TdmsChannel>().FirstOrDefault(c => c.Name == channelName);
                if (!string.IsNullOrEmpty(refChannel))
                    refCh ??= channels.Cast<TdmsChannel>().FirstOrDefault(c => c.Name == refChannel);
            }

            if (mainCh == null)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"未找到通道：{channelName}" }
                    }
                };
            }

            double result = 0;
            string resultStr = "";

            switch (setting.Mode)
            {
                case AnalyzeMode.Max:
                    result = TdmsAnalyzer.ComputeMax(mainCh);
                    resultStr = result.ToString("G6");
                    break;

                case AnalyzeMode.Min:
                    result = TdmsAnalyzer.ComputeMin(mainCh);
                    resultStr = result.ToString("G6");
                    break;

                case AnalyzeMode.Average:
                    result = TdmsAnalyzer.ComputeAverage(mainCh);
                    resultStr = result.ToString("G6");
                    break;

                case AnalyzeMode.RMS:
                    result = TdmsAnalyzer.ComputeRMS(mainCh);
                    resultStr = result.ToString("G6");
                    break;

                case AnalyzeMode.PeakWithRef:
                    if (refCh == null)
                    {
                        return new ExecutionResult
                        {
                            StepResult = new StepResult
                            {
                                Status = TestStatus.Error,
                                Error = new ErrorInfo { Message = $"PeakWithRef 模式需要参考通道，未找到：{refChannel}" }
                            }
                        };
                    }
                    var (peak, refAtPeak) = TdmsAnalyzer.ComputePeakWithRef(mainCh, refCh);
                    result = peak;
                    resultStr = $"Peak={peak:G6}, RefAt={refAtPeak:G6}";
                    if (!string.IsNullOrEmpty(refAtPeakVar))
                        context.SetVariable(refAtPeakVar, refAtPeak);
                    break;

                case AnalyzeMode.Slope:
                    if (refCh == null)
                    {
                        return new ExecutionResult
                        {
                            StepResult = new StepResult
                            {
                                Status = TestStatus.Error,
                                Error = new ErrorInfo { Message = $"Slope 模式需要参考通道作为 X 轴，未找到：{refChannel}" }
                            }
                        };
                    }
                    result = TdmsAnalyzer.ComputeSlope(refCh, mainCh);
                    resultStr = result.ToString("G6");
                    break;

                case AnalyzeMode.RangeStats:
                    if (refCh == null)
                    {
                        return new ExecutionResult
                        {
                            StepResult = new StepResult
                            {
                                Status = TestStatus.Error,
                                Error = new ErrorInfo { Message = $"RangeStats 模式需要参考通道，未找到：{refChannel}" }
                            }
                        };
                    }
                    var (avg, max, min, count) = TdmsAnalyzer.ComputeRangeStats(mainCh, refCh, setting.RangeStart, setting.RangeEnd);
                    result = avg;
                    resultStr = $"Avg={avg:G6}, Max={max:G6}, Min={min:G6}, N={count}";
                    // 额外写入区间统计变量
                    context.SetVariable($"{resultVar}_Max", max);
                    context.SetVariable($"{resultVar}_Min", min);
                    context.SetVariable($"{resultVar}_Count", count);
                    break;
            }

            context.SetVariable(resultVar, result);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = resultStr
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
                    Error = new ErrorInfo { Message = $"数据分析失败：{ex.Message}" }
                }
            };
        }
    }
}
