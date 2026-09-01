using CAN.Adapters;
using CAN.Models;

namespace CAN.Helpers;

/// <summary>
/// NI-XNET 仲裁段位时序计算器。
/// NI-XNET CAN 控制器使用 40 MHz 时钟，最小时间量子为 25 ns。
/// UI 中的寄存器值采用 NI 自定义波特率窗口显示的 BRP/SJW/TSEG1/TSEG2 编码值。
/// </summary>
public static class CanBitTimingCalculator
{
    private const double CanClockHz = 40_000_000d;
    private const int TimeQuantumNanoseconds = 25;
    private const int PreferredMaxTimeQuanta = 40;
    private const double MaxBitRateErrorRatio = 0.005;

    public static CanBitTimingConfig? Resolve(CanOpenSetting setting)
    {
        return setting.ArbitrationBitTimingMode switch
        {
            CanBitTimingMode.Automatic => null,
            CanBitTimingMode.SamplePoint => Calculate(setting.BaudRate, setting.ArbitrationSamplePoint),
            CanBitTimingMode.Registers => FromRegisters(
                setting.BaudRate,
                setting.ArbitrationBrp,
                setting.ArbitrationSjw,
                setting.ArbitrationTseg1,
                setting.ArbitrationTseg2),
            _ => throw new ArgumentOutOfRangeException(
                nameof(setting.ArbitrationBitTimingMode), setting.ArbitrationBitTimingMode, "未知的位时序配置方式")
        };
    }

    /// <summary>根据目标波特率和采样点选择最接近的 NI-XNET 寄存器组合。</summary>
    public static CanBitTimingConfig Calculate(int targetBitRate, double targetSamplePoint)
    {
        ValidateTarget(targetBitRate, targetSamplePoint);

        Candidate? best = null;
        for (int brp = 0; brp <= 511; brp++)
        {
            // 快捷模式优先使用不超过 40 TQ 的组合，与 NI 数据库编辑器常用配置一致；
            // 仍覆盖 33.333 kbit/s 等需要较大预分频的标准波特率。
            for (int totalTq = 3; totalTq <= PreferredMaxTimeQuanta; totalTq++)
            {
                double actualBitRate = CanClockHz / ((brp + 1d) * totalTq);
                double bitRateErrorRatio = Math.Abs(actualBitRate - targetBitRate) / targetBitRate;

                int tseg1 = (int)Math.Round(targetSamplePoint / 100d * totalTq - 2d,
                    MidpointRounding.AwayFromZero);
                int tseg2 = totalTq - tseg1 - 3;
                if (tseg1 is < 0 or > 255 || tseg2 is < 0 or > 127)
                    continue;

                double samplePoint = GetSamplePoint(tseg1, tseg2);
                double samplePointError = Math.Abs(samplePoint - targetSamplePoint);
                var candidate = new Candidate(
                    brp, Math.Min(4, tseg2), tseg1, tseg2,
                    actualBitRate, samplePoint, bitRateErrorRatio, samplePointError, totalTq);

                if (best == null || candidate.IsBetterThan(best.Value))
                    best = candidate;
            }
        }

        if (best == null || best.Value.BitRateErrorRatio > MaxBitRateErrorRatio)
            throw new ArgumentException(
                $"NI-XNET 无法在 40 MHz 时钟下生成 {targetBitRate} bps、{targetSamplePoint:F2}% 的仲裁段位时序");

        var value = best.Value;
        return Create(value.Brp, value.Sjw, value.Tseg1, value.Tseg2, value.ActualBitRate, value.SamplePoint);
    }

    /// <summary>验证手动寄存器值并生成 NI-XNET 64 位自定义波特率。</summary>
    public static CanBitTimingConfig FromRegisters(
        int targetBitRate, int brp, int sjw, int tseg1, int tseg2)
    {
        if (targetBitRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetBitRate), "仲裁段波特率必须大于 0");
        if (brp is < 0 or > 511)
            throw new ArgumentOutOfRangeException(nameof(brp), "BRP 必须在 0~511 之间");
        if (sjw is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(sjw), "SJW 必须在 0~127 之间");
        if (tseg1 is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(tseg1), "TSEG1 必须在 0~255 之间");
        if (tseg2 is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(tseg2), "TSEG2 必须在 0~127 之间");
        if (sjw > tseg2)
            throw new ArgumentException("SJW 不能大于 TSEG2");

        int totalTq = tseg1 + tseg2 + 3;
        double actualBitRate = CanClockHz / ((brp + 1d) * totalTq);
        double errorRatio = Math.Abs(actualBitRate - targetBitRate) / targetBitRate;
        if (errorRatio > MaxBitRateErrorRatio)
        {
            throw new ArgumentException(
                $"BRP={brp}, TSEG1={tseg1}, TSEG2={tseg2} 得到 {actualBitRate:F0} bps，" +
                $"与配置的 {targetBitRate} bps 偏差超过 {MaxBitRateErrorRatio:P1}");
        }

        return Create(brp, sjw, tseg1, tseg2, actualBitRate, GetSamplePoint(tseg1, tseg2));
    }

    public static string Describe(CanBitTimingConfig timing) =>
        $"BRP={timing.Brp}, SJW={timing.Sjw}, TSEG1={timing.Tseg1}, TSEG2={timing.Tseg2}, " +
        $"实际波特率={timing.ActualBitRate:F0} bps, 采样点={timing.SamplePoint:F2}%, " +
        $"NI值=0x{timing.NiXnetBaudRate64:X16}";

    private static void ValidateTarget(int targetBitRate, double targetSamplePoint)
    {
        if (targetBitRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetBitRate), "仲裁段波特率必须大于 0");
        if (double.IsNaN(targetSamplePoint) || double.IsInfinity(targetSamplePoint) ||
            targetSamplePoint is < 7.5 or > 97.5)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSamplePoint), "采样点必须在 7.5%~97.5% 之间");
        }
    }

    private static CanBitTimingConfig Create(
        int brp, int sjw, int tseg1, int tseg2, double actualBitRate, double samplePoint)
    {
        ulong tqNanoseconds = (ulong)((brp + 1) * TimeQuantumNanoseconds);
        ulong customBaudRate =
            (tqNanoseconds << 32) |
            0xA0000000UL |
            ((ulong)(uint)sjw << 16) |
            ((ulong)(uint)tseg1 << 8) |
            (uint)tseg2;

        return new CanBitTimingConfig
        {
            Brp = brp,
            Sjw = sjw,
            Tseg1 = tseg1,
            Tseg2 = tseg2,
            ActualBitRate = actualBitRate,
            SamplePoint = samplePoint,
            NiXnetBaudRate64 = customBaudRate
        };
    }

    private static double GetSamplePoint(int tseg1, int tseg2) =>
        (tseg1 + 2d) / (tseg1 + tseg2 + 3d) * 100d;

    private readonly record struct Candidate(
        int Brp,
        int Sjw,
        int Tseg1,
        int Tseg2,
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
