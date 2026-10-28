using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

public sealed class TrajectoryCursorTests
{
    private static CanTrajectory Table => new(Enumerable.Range(0, 5).Select(i => new TimedCanPoint(i * 10, [new(0x201, [(byte)i])])).ToArray(), 60);
    private static CycleOptions Options(TrajectoryMissPolicy policy, int max = 20) => new(10)
    { MissPolicy = policy, MaxSkippedPoints = max, LatenessWindowMs = 10, HoldTimeoutMs = 8 };

    [Fact]
    public void Catchup_selects_only_the_latest_due_point_and_records_skips_after_submission()
    {
        var cursor = new TrajectoryCursor(Table, Options(TrajectoryMissPolicy.SkipAndCatchUp));
        cursor.Submitted(cursor.Select(0)!);
        var selected = cursor.Select(25)!;
        Assert.Equal(2, selected.Index); Assert.Equal(1, selected.Skipped); Assert.Equal(0, cursor.SkippedPoints);
        cursor.Submitted(selected);
        Assert.Equal(1, cursor.SkippedPoints); Assert.Equal(2, cursor.ExecutedPoints); Assert.Equal(30, cursor.NextDueMs);
        Assert.Null(cursor.Select(26));
    }
    [Fact]
    public void Terminal_must_be_submitted_and_is_not_invented_after_the_total_deadline()
    {
        var cursor = new TrajectoryCursor(Table, Options(TrajectoryMissPolicy.SkipAndCatchUp));
        cursor.Submitted(cursor.Select(0)!);
        Assert.Throws<TimeoutException>(() => cursor.Select(60)); Assert.False(cursor.TerminalSubmitted);
        cursor = new TrajectoryCursor(Table, Options(TrajectoryMissPolicy.SkipAndCatchUp));
        cursor.Submitted(cursor.Select(0)!); // A different run; never roll back the clock after a deadline failure.
        var terminal = cursor.Select(40)!; Assert.Equal(4, terminal.Index);
        cursor.Submitted(terminal); Assert.True(cursor.TerminalSubmitted); Assert.True(cursor.Complete);
        Assert.Equal(3, cursor.SkippedPoints); Assert.Equal(2, cursor.ExecutedPoints);
    }
    [Fact]
    public void Skip_limit_is_cumulative_and_checked_before_sending()
    {
        var cursor = new TrajectoryCursor(Table, Options(TrajectoryMissPolicy.SkipAndCatchUp, 1));
        cursor.Submitted(cursor.Select(0)!); cursor.Submitted(cursor.Select(25)!);
        Assert.Throws<TimeoutException>(() => cursor.Select(40));
        Assert.Equal(1, cursor.SkippedPoints); Assert.False(cursor.TerminalSubmitted);
    }
    [Fact]
    public void Initial_point_and_stale_terminal_are_not_blindly_skipped_or_forced()
    {
        var cursor = new TrajectoryCursor(Table, Options(TrajectoryMissPolicy.SkipAndCatchUp));
        Assert.Throws<TimeoutException>(() => cursor.Select(25));
        cursor = new TrajectoryCursor(Table, Options(TrajectoryMissPolicy.SkipAndCatchUp));
        cursor.Submitted(cursor.Select(0)!);
        Assert.Throws<TimeoutException>(() => cursor.Select(55)); Assert.False(cursor.TerminalSubmitted);
    }
    [Fact]
    public void Hold_repeats_only_the_last_submitted_bytes_and_does_not_refresh_its_deadline()
    {
        var cursor = new TrajectoryCursor(Table, Options(TrajectoryMissPolicy.HoldLast));
        cursor.Submitted(cursor.Select(0)!);
        var hold = cursor.Select(22)!; Assert.True(hold.Hold); Assert.Equal(0, hold.Frames[0].Data[0]);
        cursor.Submitted(hold); Assert.Equal(2, cursor.SkippedPoints); Assert.Equal(1, cursor.ExecutedPoints);
        Assert.Throws<TimeoutException>(() => cursor.Select(30)); // fixed 22+8, not renewed by another cycle
    }
}
