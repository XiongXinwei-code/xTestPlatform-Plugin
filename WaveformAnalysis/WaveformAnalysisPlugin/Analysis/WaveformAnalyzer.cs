using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace WaveformAnalysisPlugin.Analysis
{
    /// <summary>
    /// 波形分析结果
    /// </summary>
    public class WaveformAnalysisResult
    {
        /// <summary>原始波形数据</summary>
        public double[] OriginalData { get; set; } = [];

        /// <summary>处理后的数据（滤波结果或频谱幅值）</summary>
        public double[] ProcessedData { get; set; } = [];

        /// <summary>频率轴（FFT 时使用）</summary>
        public double[] FrequencyAxis { get; set; } = [];

        /// <summary>峰值索引列表</summary>
        public List<int> PeakIndices { get; set; } = [];

        /// <summary>谷值索引列表</summary>
        public List<int> ValleyIndices { get; set; } = [];

        /// <summary>统计信息</summary>
        public StatisticsInfo? Statistics { get; set; }
    }

    public class StatisticsInfo
    {
        public double Mean { get; set; }
        public double RMS { get; set; }
        public double StandardDeviation { get; set; }
        public double Max { get; set; }
        public double Min { get; set; }
        public double PeakToPeak { get; set; }
    }

    /// <summary>
    /// 波形分析引擎 — 提供 FFT、峰值检测、统计、滤波功能
    /// </summary>
    public static class WaveformAnalyzer
    {
        /// <summary>FFT 频谱分析</summary>
        public static WaveformAnalysisResult PerformFFT(double[] data, double sampleRate)
        {
            int n = NextPowerOfTwo(data.Length);
            var complex = new Complex[n];
            for (int i = 0; i < data.Length; i++)
                complex[i] = new Complex(data[i], 0);

            Fourier.Forward(complex, FourierOptions.Matlab);

            int halfN = n / 2;
            var magnitude = new double[halfN];
            var freqAxis = new double[halfN];
            double freqResolution = sampleRate / n;

            for (int i = 0; i < halfN; i++)
            {
                magnitude[i] = complex[i].Magnitude * 2.0 / n;
                freqAxis[i] = i * freqResolution;
            }

            return new WaveformAnalysisResult
            {
                OriginalData = data,
                ProcessedData = magnitude,
                FrequencyAxis = freqAxis
            };
        }

        /// <summary>峰值/谷值检测</summary>
        public static WaveformAnalysisResult DetectPeaks(double[] data, double threshold)
        {
            var peaks = new List<int>();
            var valleys = new List<int>();

            for (int i = 1; i < data.Length - 1; i++)
            {
                if (data[i] > data[i - 1] && data[i] > data[i + 1] && data[i] >= threshold)
                    peaks.Add(i);
                if (data[i] < data[i - 1] && data[i] < data[i + 1] && data[i] <= -threshold)
                    valleys.Add(i);
            }

            return new WaveformAnalysisResult
            {
                OriginalData = data,
                ProcessedData = data,
                PeakIndices = peaks,
                ValleyIndices = valleys
            };
        }

        /// <summary>统计分析</summary>
        public static WaveformAnalysisResult ComputeStatistics(double[] data)
        {
            double mean = data.Average();
            double rms = Math.Sqrt(data.Select(x => x * x).Average());
            double std = Math.Sqrt(data.Select(x => (x - mean) * (x - mean)).Average());
            double max = data.Max();
            double min = data.Min();

            return new WaveformAnalysisResult
            {
                OriginalData = data,
                ProcessedData = data,
                Statistics = new StatisticsInfo
                {
                    Mean = mean,
                    RMS = rms,
                    StandardDeviation = std,
                    Max = max,
                    Min = min,
                    PeakToPeak = max - min
                }
            };
        }

        /// <summary>低通滤波（简单移动平均实现）</summary>
        public static WaveformAnalysisResult LowPassFilter(double[] data, double sampleRate, double cutoffFreq, int order)
        {
            int windowSize = Math.Max(1, (int)(sampleRate / cutoffFreq / 2));
            var filtered = MovingAverage(data, windowSize, order);
            return new WaveformAnalysisResult
            {
                OriginalData = data,
                ProcessedData = filtered
            };
        }

        /// <summary>高通滤波（原始信号 - 低通）</summary>
        public static WaveformAnalysisResult HighPassFilter(double[] data, double sampleRate, double cutoffFreq, int order)
        {
            var lowPass = LowPassFilter(data, sampleRate, cutoffFreq, order);
            var filtered = new double[data.Length];
            for (int i = 0; i < data.Length; i++)
                filtered[i] = data[i] - lowPass.ProcessedData[i];

            return new WaveformAnalysisResult
            {
                OriginalData = data,
                ProcessedData = filtered
            };
        }

        /// <summary>带通滤波</summary>
        public static WaveformAnalysisResult BandPassFilter(double[] data, double sampleRate, double lowCutoff, double highCutoff, int order)
        {
            var highPassed = HighPassFilter(data, sampleRate, lowCutoff, order);
            var bandPassed = LowPassFilter(highPassed.ProcessedData, sampleRate, highCutoff, order);
            return new WaveformAnalysisResult
            {
                OriginalData = data,
                ProcessedData = bandPassed.ProcessedData
            };
        }

        #region Helpers

        private static double[] MovingAverage(double[] data, int windowSize, int passes)
        {
            var result = (double[])data.Clone();
            for (int p = 0; p < passes; p++)
            {
                var temp = new double[result.Length];
                for (int i = 0; i < result.Length; i++)
                {
                    int start = Math.Max(0, i - windowSize / 2);
                    int end = Math.Min(result.Length - 1, i + windowSize / 2);
                    double sum = 0;
                    for (int j = start; j <= end; j++)
                        sum += result[j];
                    temp[i] = sum / (end - start + 1);
                }
                result = temp;
            }
            return result;
        }

        private static int NextPowerOfTwo(int n)
        {
            int power = 1;
            while (power < n) power <<= 1;
            return power;
        }

        #endregion
    }
}
