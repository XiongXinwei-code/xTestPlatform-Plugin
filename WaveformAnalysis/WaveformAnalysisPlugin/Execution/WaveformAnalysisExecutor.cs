using MessagePack;
using MessagePack.Resolvers;
using System.Text.Json;
using WaveformAnalysisPlugin.Analysis;
using WaveformAnalysisPlugin.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace WaveformAnalysisPlugin.Execution
{
    public sealed class WaveformAnalysisExecutor : IStepExecutor
    {
        private static readonly MessagePackSerializerOptions _msgpackOptions =
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(ContractlessStandardResolver.Instance);

        public async Task<ExecutionResult> ExecuteAsync(
            IExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            // ── 1. 反序列化设置 ──────────────────────────────────────────
            var rawSetting = context.CurrentStep?.Step?.StepSetting?.Setting;
            WaveformAnalysisSetting setting;
            try
            {
                setting = rawSetting is { Length: > 0 }
                    ? MessagePackSerializer.Deserialize<WaveformAnalysisSetting>(rawSetting, _msgpackOptions)
                    : new WaveformAnalysisSetting();
            }
            catch (Exception ex)
            {
                return ErrorResult($"反序列化设置失败: {ex.Message}");
            }

            // ── 2. 从步骤变量获取输入数据 ────────────────────────────────
            if (string.IsNullOrWhiteSpace(setting.InputVariableName))
                return ErrorResult("未配置输入变量名");

            var inputVar = context.GetVariable(setting.InputVariableName);
            if (inputVar == null)
                return ErrorResult($"变量 '{setting.InputVariableName}' 不存在");

            double[] data;
            try
            {
                data = inputVar switch
                {
                    List<double> list => list.ToArray(),
                    double[] arr => arr,
                    _ => throw new InvalidCastException($"变量类型不支持: {inputVar.GetType().Name}")
                };
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.Message);
            }

            if (data.Length == 0)
                return ErrorResult("输入数据为空");

            // ── 3. 执行分析 ──────────────────────────────────────────────
            WaveformAnalysisResult result;
            try
            {
                result = setting.AnalysisType switch
                {
                    AnalysisType.FFT => WaveformAnalyzer.PerformFFT(data, setting.SampleRate),
                    AnalysisType.PeakDetection => WaveformAnalyzer.DetectPeaks(data, setting.PeakThreshold),
                    AnalysisType.Statistics => WaveformAnalyzer.ComputeStatistics(data),
                    AnalysisType.LowPassFilter => WaveformAnalyzer.LowPassFilter(data, setting.SampleRate, setting.CutoffFrequencyLow, setting.FilterOrder),
                    AnalysisType.HighPassFilter => WaveformAnalyzer.HighPassFilter(data, setting.SampleRate, setting.CutoffFrequencyHigh, setting.FilterOrder),
                    AnalysisType.BandPassFilter => WaveformAnalyzer.BandPassFilter(data, setting.SampleRate, setting.CutoffFrequencyLow, setting.CutoffFrequencyHigh, setting.FilterOrder),
                    _ => throw new NotSupportedException($"不支持的分析类型: {setting.AnalysisType}")
                };
            }
            catch (Exception ex)
            {
                return ErrorResult($"分析执行失败: {ex.Message}");
            }

            // ── 4. 将结果写入输出变量 ────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(setting.OutputVariableName))
            {
                context.SetVariable(setting.OutputVariableName, result.ProcessedData.ToList());
            }

            // ── 5. 将完整结果序列化存入特殊变量（供 UI 层弹窗读取）────────
            var resultJson = JsonSerializer.Serialize(result);
            context.SetVariable($"__{setting.OutputVariableName}_ChartData", resultJson);

            var message = setting.AnalysisType switch
            {
                AnalysisType.Statistics => $"均值={result.Statistics!.Mean:F4}, RMS={result.Statistics.RMS:F4}, 标准差={result.Statistics.StandardDeviation:F4}",
                AnalysisType.PeakDetection => $"检测到 {result.PeakIndices.Count} 个峰值, {result.ValleyIndices.Count} 个谷值",
                _ => $"分析完成，输出 {result.ProcessedData.Length} 个数据点"
            };

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed }
            };
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
}
