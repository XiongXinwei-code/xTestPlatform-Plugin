using System.Diagnostics;
using CAN.Adapters.Zlg;
using Xunit;

namespace CAN.ContractTests;

public class ZlgClockTests
{
    [Fact]
    public void CaptureLowerBoundUsesEarlierSubmissionNotDequeueOrInterpolation()
    {
        var clock = new ZlgEchoClock(); var before = Stopwatch.GetTimestamp();
        Assert.Null(clock.LowerBound(100, before));
        clock.Add(100, before, before + 10);
        Assert.Null(clock.LowerBound(99, before + 20));
        Assert.Equal(before, clock.LowerBound(101, before + 20));
        Assert.False(clock.IsCurrent(clock.Id, before + Stopwatch.Frequency));
    }

    [Fact]
    public void CounterJumpInvalidatesClockAndNeedsLaterEchoToEstablishNewAnchor()
    {
        var clock = new ZlgEchoClock(); var now = Stopwatch.GetTimestamp();
        clock.Add(100, now, now + 10); var oldId = clock.Id;
        clock.Add(99, now + 20, now + 30);
        Assert.NotEqual(oldId, clock.Id);
        Assert.Null(clock.LowerBound(100, now + 40));
        clock.Add(110, now + 50, now + 60);
        Assert.Equal(now + 50, clock.LowerBound(111, now + 70));
    }
}
