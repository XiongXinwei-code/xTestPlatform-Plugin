namespace CAN.Helpers;

/// <summary>厂商适配器内部使用的 CAN 控制器位时序结果；不作为步骤 UI 配置公开。</summary>
internal sealed class CanControllerTiming
{
    public int Prescaler { get; init; }
    public int Sjw { get; init; }
    public int Tseg1 { get; init; }
    public int Tseg2 { get; init; }
    public double ActualBitRate { get; init; }
    public double SamplePoint { get; init; }
}

/// <summary>把厂商无关的采样点百分比换算成具体控制器的位时序。</summary>
internal static class CanSamplePointCalculator
{
    private const double MaxBitRateErrorRatio = 0.005;

    public static CanControllerTiming Calculate(
        int clockHz,
        int targetBitRate,
        double targetSamplePoint,
        int maxPrescaler,
        int maxTseg1,
        int maxTseg2,
        int maxSjw,
        int maxTotalTq)
    {
        Validate(targetBitRate, targetSamplePoint);
        Candidate? best = null;

        int controllerMaxTq = Math.Min(maxTotalTq, 1 + maxTseg1 + maxTseg2);
        for (int prescaler = 1; prescaler <= maxPrescaler; prescaler++)
        {
            for (int totalTq = 3; totalTq <= controllerMaxTq; totalTq++)
            {
                int tseg1 = (int)Math.Round(targetSamplePoint / 100d * totalTq - 1d,
                    MidpointRounding.AwayFromZero);
                int tseg2 = totalTq - 1 - tseg1;
                if (tseg1 is < 1 || tseg1 > maxTseg1 || tseg2 is < 1 || tseg2 > maxTseg2)
                    continue;

                double actualBitRate = (double)clockHz / (prescaler * totalTq);
                double bitRateError = Math.Abs(actualBitRate - targetBitRate) / targetBitRate;
                double samplePoint = (1d + tseg1) / totalTq * 100d;
                var candidate = new Candidate(
                    prescaler, tseg1, tseg2, Math.Min(Math.Min(1, maxSjw), tseg2),
                    actualBitRate, samplePoint, bitRateError,
                    Math.Abs(samplePoint - targetSamplePoint), totalTq);

                if (best == null || candidate.IsBetterThan(best.Value))
                    best = candidate;
            }
        }

        if (best == null || best.Value.BitRateErrorRatio > MaxBitRateErrorRatio)
        {
            throw new ArgumentException(
                $"控制器无法生成 {targetBitRate} bps、{targetSamplePoint:F2}% 的仲裁段位时序");
        }

        return ToResult(best.Value);
    }

    /// <summary>
    /// 用于由驱动根据 BitRate 选择预分频的接口，只计算能表达目标采样点的段长度。
    /// </summary>
    public static CanControllerTiming CalculateSegments(
        int targetBitRate,
        double targetSamplePoint,
        int maxTseg1,
        int maxTseg2,
        int maxSjw,
        int maxTotalTq)
    {
        Validate(targetBitRate, targetSamplePoint);
        Candidate? best = null;
        int controllerMaxTq = Math.Min(maxTotalTq, 1 + maxTseg1 + maxTseg2);

        for (int totalTq = 3; totalTq <= controllerMaxTq; totalTq++)
        {
            int tseg1 = (int)Math.Round(targetSamplePoint / 100d * totalTq - 1d,
                MidpointRounding.AwayFromZero);
            int tseg2 = totalTq - 1 - tseg1;
            if (tseg1 is < 1 || tseg1 > maxTseg1 || tseg2 is < 1 || tseg2 > maxTseg2)
                continue;

            double samplePoint = (1d + tseg1) / totalTq * 100d;
            var candidate = new Candidate(
                1, tseg1, tseg2, Math.Min(Math.Min(1, maxSjw), tseg2),
                targetBitRate, samplePoint, 0, Math.Abs(samplePoint - targetSamplePoint), totalTq);
            if (best == null || candidate.IsBetterThan(best.Value))
                best = candidate;
        }

        if (best == null)
            throw new ArgumentException($"控制器无法表达 {targetSamplePoint:F2}% 的仲裁段采样点");

        return ToResult(best.Value);
    }

    public static ushort ToSja1000Btr(int targetBitRate, double targetSamplePoint)
    {
        return ToSja1000Btr(CalculateSja1000(targetBitRate, targetSamplePoint));
    }

    public static CanControllerTiming CalculateSja1000(int targetBitRate, double targetSamplePoint) =>
        Calculate(
            8_000_000, targetBitRate, targetSamplePoint,
            maxPrescaler: 64, maxTseg1: 16, maxTseg2: 8, maxSjw: 4, maxTotalTq: 25);

    public static ushort ToSja1000Btr(CanControllerTiming timing)
    {
        byte btr0 = (byte)(((timing.Sjw - 1) << 6) | (timing.Prescaler - 1));
        byte btr1 = (byte)(((timing.Tseg2 - 1) << 4) | (timing.Tseg1 - 1));
        return (ushort)((btr0 << 8) | btr1);
    }

    public static string Describe(CanControllerTiming timing) =>
        $"实际波特率={timing.ActualBitRate:F0} bps, 实际采样点={timing.SamplePoint:F2}%";

    private static void Validate(int targetBitRate, double targetSamplePoint)
    {
        if (targetBitRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetBitRate), "仲裁段波特率必须大于 0");
        if (double.IsNaN(targetSamplePoint) || double.IsInfinity(targetSamplePoint) ||
            targetSamplePoint is < 7.5 or > 97.5)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSamplePoint), "采样点必须在 7.5%~97.5% 之间");
        }
    }

    private static CanControllerTiming ToResult(Candidate candidate) => new()
    {
        Prescaler = candidate.Prescaler,
        Sjw = candidate.Sjw,
        Tseg1 = candidate.Tseg1,
        Tseg2 = candidate.Tseg2,
        ActualBitRate = candidate.ActualBitRate,
        SamplePoint = candidate.SamplePoint
    };

    private readonly record struct Candidate(
        int Prescaler,
        int Tseg1,
        int Tseg2,
        int Sjw,
        double ActualBitRate,
        double SamplePoint,
        double BitRateErrorRatio,
        double SamplePointError,
        int TotalTq)
    {
        public bool IsBetterThan(Candidate other)
        {
            const double epsilon = 1e-12;
            if (BitRateErrorRatio + epsilon < other.BitRateErrorRatio) return true;
            if (Math.Abs(BitRateErrorRatio - other.BitRateErrorRatio) > epsilon) return false;
            if (SamplePointError + epsilon < other.SamplePointError) return true;
            if (Math.Abs(SamplePointError - other.SamplePointError) > epsilon) return false;
            return TotalTq > other.TotalTq;
        }
    }
}
