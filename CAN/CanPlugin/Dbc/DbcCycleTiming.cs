namespace CAN.Dbc;

/// <summary>DBC 周期来源与手工备用值的共同规则，供执行器和编辑校验使用。</summary>
public static class DbcCycleTiming
{
    public static int Resolve(DbcMessage message, bool useDbcCycleTime, int manualOrFallbackMs)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (manualOrFallbackMs <= 0) throw new ArgumentException($"0x{message.Id:X} 的手动/备用周期必须大于 0 ms");
        return useDbcCycleTime && message.CycleTimeMs is > 0 ? message.CycleTimeMs.Value : manualOrFallbackMs;
    }
}

/// <summary>每行独立截止时间；完成后跳过已过期的节拍，不密集补发积压帧。</summary>
internal sealed class DbcCycleSchedule
{
    private readonly int[] _periods;
    private readonly double[] _next;
    internal DbcCycleSchedule(IEnumerable<int> periods)
    {
        _periods = periods.ToArray();
        if (_periods.Length == 0 || _periods.Any(period => period <= 0)) throw new ArgumentException("周期列表不能为空且各周期必须大于 0");
        _next = _periods.Select(period => (double)period).ToArray();
    }
    internal double DelayMs(double nowMs) => Math.Max(0, _next.Min() - nowMs);
    internal int[] Due(double nowMs) => Enumerable.Range(0, _periods.Length).Where(i => nowMs >= _next[i]).ToArray();
    internal void Complete(IEnumerable<int> sent, double completedMs)
    {
        foreach (int i in sent)
            _next[i] = (Math.Floor(completedMs / _periods[i]) + 1) * _periods[i];
    }
}
