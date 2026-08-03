using NationalInstruments.Tdms;

namespace NiDaq.Helpers;

/// <summary>TDMS 文件流式分析器，分块读取 O(1) 内存</summary>
public static class TdmsAnalyzer
{
    private const int ChunkSize = 10000;

    public static double ComputeMax(TdmsChannel channel)
    {
        double max = double.MinValue;
        long total = channel.DataCount;
        for (long offset = 0; offset < total; offset += ChunkSize)
        {
            int len = (int)Math.Min(ChunkSize, total - offset);
            var data = channel.ReadData<double>(offset, len);
            foreach (var v in data) if (v > max) max = v;
        }
        return max;
    }

    public static double ComputeMin(TdmsChannel channel)
    {
        double min = double.MaxValue;
        long total = channel.DataCount;
        for (long offset = 0; offset < total; offset += ChunkSize)
        {
            int len = (int)Math.Min(ChunkSize, total - offset);
            var data = channel.ReadData<double>(offset, len);
            foreach (var v in data) if (v < min) min = v;
        }
        return min;
    }

    public static double ComputeAverage(TdmsChannel channel)
    {
        double sum = 0; long count = 0;
        long total = channel.DataCount;
        for (long offset = 0; offset < total; offset += ChunkSize)
        {
            int len = (int)Math.Min(ChunkSize, total - offset);
            var data = channel.ReadData<double>(offset, len);
            foreach (var v in data) { sum += v; count++; }
        }
        return count > 0 ? sum / count : 0;
    }

    public static double ComputeRMS(TdmsChannel channel)
    {
        double sumSq = 0; long count = 0;
        long total = channel.DataCount;
        for (long offset = 0; offset < total; offset += ChunkSize)
        {
            int len = (int)Math.Min(ChunkSize, total - offset);
            var data = channel.ReadData<double>(offset, len);
            foreach (var v in data) { sumSq += v * v; count++; }
        }
        return count > 0 ? Math.Sqrt(sumSq / count) : 0;
    }

    /// <summary>找主通道峰值及对应参考通道值</summary>
    public static (double peak, double refAtPeak) ComputePeakWithRef(TdmsChannel mainChannel, TdmsChannel refChannel)
    {
        double peak = double.MinValue;
        double refAtPeak = 0;
        long total = mainChannel.DataCount;
        for (long offset = 0; offset < total; offset += ChunkSize)
        {
            int len = (int)Math.Min(ChunkSize, total - offset);
            var mainData = mainChannel.ReadData<double>(offset, len);
            var refData = refChannel.ReadData<double>(offset, len);
            for (int i = 0; i < mainData.Length; i++)
            {
                if (mainData[i] > peak)
                {
                    peak = mainData[i];
                    refAtPeak = refData[i];
                }
            }
        }
        return (peak, refAtPeak);
    }

    /// <summary>线性拟合斜率（在线最小二乘）</summary>
    public static double ComputeSlope(TdmsChannel xChannel, TdmsChannel yChannel)
    {
        double sx = 0, sy = 0, sxy = 0, sx2 = 0;
        long n = 0;
        long total = xChannel.DataCount;
        for (long offset = 0; offset < total; offset += ChunkSize)
        {
            int len = (int)Math.Min(ChunkSize, total - offset);
            var xData = xChannel.ReadData<double>(offset, len);
            var yData = yChannel.ReadData<double>(offset, len);
            for (int i = 0; i < xData.Length; i++)
            {
                sx += xData[i]; sy += yData[i];
                sxy += xData[i] * yData[i];
                sx2 += xData[i] * xData[i];
                n++;
            }
        }
        double denom = n * sx2 - sx * sx;
        return denom != 0 ? (n * sxy - sx * sy) / denom : 0;
    }

    /// <summary>区间统计：在参考通道值在 [rangeStart, rangeEnd] 内的样本上计算主通道统计</summary>
    public static (double avg, double max, double min, long count) ComputeRangeStats(
        TdmsChannel mainChannel, TdmsChannel refChannel, double rangeStart, double rangeEnd)
    {
        double sum = 0, max = double.MinValue, min = double.MaxValue;
        long count = 0;
        long total = mainChannel.DataCount;
        for (long offset = 0; offset < total; offset += ChunkSize)
        {
            int len = (int)Math.Min(ChunkSize, total - offset);
            var mainData = mainChannel.ReadData<double>(offset, len);
            var refData = refChannel.ReadData<double>(offset, len);
            for (int i = 0; i < mainData.Length; i++)
            {
                if (refData[i] >= rangeStart && refData[i] <= rangeEnd)
                {
                    double v = mainData[i];
                    sum += v; count++;
                    if (v > max) max = v;
                    if (v < min) min = v;
                }
            }
        }
        double avg = count > 0 ? sum / count : 0;
        return (avg, max, min, count);
    }
}
