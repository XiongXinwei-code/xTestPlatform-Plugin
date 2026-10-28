namespace CAN.Runtime;

/// <summary>只按时间选择不可变原始帧；不解析电机单位、不插值、不证明运动连续性。</summary>
internal sealed class TrajectoryCursor
{
    internal sealed record Selection(int Index, OrderedFrame[] Frames, int Skipped, bool Hold,
        int NextIndex, double NextDueMs, double LatenessMs);
    private readonly CanTrajectory _table;
    private readonly CycleOptions _options;
    private readonly int _window;
    private int _next, _last = -1;
    private double? _holdDeadline;
    internal int ExecutedPoints { get; private set; }
    internal int SkippedPoints { get; private set; }
    internal int HeldCycles { get; private set; }
    internal double LastLatenessMs { get; private set; }
    internal bool TerminalSubmitted => _last == _table.Points.Length - 1;
    internal bool Complete => _next == _table.Points.Length;
    internal double NextDueMs { get; private set; }
    internal int NextIndex => _next;

    internal TrajectoryCursor(CanTrajectory table, CycleOptions options)
    {
        if (options.MissPolicy is not (TrajectoryMissPolicy.SkipAndCatchUp or TrajectoryMissPolicy.HoldLast) ||
            options.PeriodMs <= 0 || options.LatenessWindowMs < 0 || options.MaxSkippedPoints < 0 || options.HoldTimeoutMs <= 0)
            throw new ArgumentException("点表迟到策略/窗口/跳点上限/保持期限无效");
        _table = table; _options = options;
        _window = options.LatenessWindowMs == 0 ? options.PeriodMs : options.LatenessWindowMs;
    }

    internal Selection? Select(double now)
    {
        if (!double.IsFinite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        if (Complete) return null;
        if (now >= _table.DurationMs) throw new TimeoutException("轨迹总时长已到，终点未提交；不在期限外强发终点");
        if (_holdDeadline is double deadline && now >= deadline) throw new TimeoutException("保持上次目标已达到有限期限，停止运动发送");
        int latest = LatestDue(now);
        if (latest < _next) return null;
        if (_next == 0 && latest != 0) throw new TimeoutException("首个轨迹点尚未提交，不能跳过初始点启动");
        double currentLate = now - _table.Points[_next].OffsetMs;
        LastLatenessMs = currentLate;
        if (_options.MissPolicy == TrajectoryMissPolicy.HoldLast && (latest != _next || currentLate > _window))
        {
            if (_last < 0) throw new TimeoutException("没有已提交目标，不能保持上次值");
            // Never omit the terminal point. If it is too old, hold is bounded
            // and ends in a stop rather than pretending it was delivered.
            int next = Math.Min(latest + 1, _table.Points.Length - 1);
            int skipped = next - _next; CheckSkipped(skipped);
            _holdDeadline ??= Math.Min(_table.DurationMs, now + _options.HoldTimeoutMs);
            double nextDue = Math.Min(Math.Min(now + _options.PeriodMs, _holdDeadline.Value),
                _table.Points[next].OffsetMs > now ? _table.Points[next].OffsetMs : double.PositiveInfinity);
            return new(_last, _table.Points[_last].Frames, skipped, true, next, nextDue, currentLate);
        }
        double late = now - _table.Points[latest].OffsetMs;
        LastLatenessMs = late;
        if (late > _window) throw new TimeoutException($"轨迹点{latest}（Offset={_table.Points[latest].OffsetMs}ms）迟到 {late:F4} ms，超过窗口 {_window} ms；未提交过期点");
        int omitted = latest - _next; CheckSkipped(omitted);
        return new(latest, _table.Points[latest].Frames, omitted, false, latest + 1,
            latest + 1 < _table.Points.Length ? _table.Points[latest + 1].OffsetMs : _table.DurationMs, late);
    }

    internal void Submitted(Selection selection)
    {
        _next = selection.NextIndex; NextDueMs = selection.NextDueMs;
        SkippedPoints += selection.Skipped; LastLatenessMs = selection.LatenessMs;
        if (selection.Hold) HeldCycles++;
        else { _last = selection.Index; ExecutedPoints++; _holdDeadline = null; }
    }
    private void CheckSkipped(int additional)
    {
        if (additional < 0 || (long)SkippedPoints + additional > _options.MaxSkippedPoints)
            throw new TimeoutException($"累计跳点将超过配置上限 {_options.MaxSkippedPoints}；已跳={SkippedPoints}，本次拟跳={additional}");
    }
    private int LatestDue(double now)
    {
        int left = 0, right = _table.Points.Length - 1, result = -1;
        while (left <= right)
        {
            int middle = left + (right - left) / 2;
            if (_table.Points[middle].OffsetMs <= now) { result = middle; left = middle + 1; }
            else right = middle - 1;
        }
        return result;
    }
}
