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

            // ── 4. 将结果写入用户映射的输出变量 ────────────────────────
            var map = setting.OutputVariableMap;
            if (map.Count > 0)
            {
                var outputValues = GetOutputValues(setting.AnalysisType, result);
                foreach (var (fieldName, userVarName) in map)
                {
                    if (!string.IsNullOrWhiteSpace(userVarName) && outputValues.TryGetValue(fieldName, out var value))
                        context.SetVariable(userVarName, value);
                }
            }

            // ── 5. 将完整结果序列化存入特殊变量（供 UI 层弹窗读取）────────
            var resultJson = JsonSerializer.Serialize(result);
            context.SetVariable("__WaveformAnalysis_ChartData", resultJson);

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

        /// <summary>
        /// 根据分析类型，提取所有可输出的结果字段
        /// </summary>
        private static Dictionary<string, object> GetOutputValues(AnalysisType type, WaveformAnalysisResult result)
        {
            var values = new Dictionary<string, object>();
            switch (type)
            {
                case AnalysisType.Statistics:
                    var s = result.Statistics!;
                    values["Mean"] = s.Mean;
                    values["RMS"] = s.RMS;
                    values["StdDev"] = s.StandardDeviation;
                    values["Max"] = s.Max;
                    values["Min"] = s.Min;
                    values["PeakToPeak"] = s.PeakToPeak;
                    break;
                case AnalysisType.PeakDetection:
                    values["FilteredData"] = result.ProcessedData.ToList();
                    values["PeakCount"] = result.PeakIndices.Count;
                    values["ValleyCount"] = result.ValleyIndices.Count;
                    values["PeakIndices"] = result.PeakIndices;
                    values["ValleyIndices"] = result.ValleyIndices;
                    break;
                case AnalysisType.FFT:
                    values["Magnitude"] = result.ProcessedData.ToList();
                    values["FrequencyAxis"] = result.FrequencyAxis.ToList();
                    break;
                default: // 滤波
                    values["FilteredData"] = result.ProcessedData.ToList();
                    break;
            }
            return values;
        }

        /// <summary>
        /// 获取指定分析类型可输出的字段定义（字段名 + 类型描述），供 UI 显示
        /// </summary>
        public static IReadOnlyList<(string FieldName, string TypeDesc)> GetOutputFields(AnalysisType type)
        {
            return type switch
            {
                AnalysisType.Statistics => [
                    ("Mean", "double - 均值"),
                    ("RMS", "double - 均方根"),
                    ("StdDev", "double - 标准差"),
                    ("Max", "double - 最大值"),
                    ("Min", "double - 最小值"),
                    ("PeakToPeak", "double - 峰峰值")
                ],
                AnalysisType.PeakDetection => [
                    ("FilteredData", "List<double> - 数据"),
                    ("PeakCount", "int - 峰值数量"),
                    ("ValleyCount", "int - 谷值数量"),
                    ("PeakIndices", "List<int> - 峰值索引"),
                    ("ValleyIndices", "List<int> - 谷值索引")
                ],
                AnalysisType.FFT => [
                    ("Magnitude", "List<double> - 频谱幅值"),
                    ("FrequencyAxis", "List<double> - 频率轴")
                ],
                _ => [
                    ("FilteredData", "List<double> - 滤波后数据")
                ]
            };
        }
    }
}
