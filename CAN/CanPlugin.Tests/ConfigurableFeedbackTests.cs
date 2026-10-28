using System.Diagnostics;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

public class ConfigurableFeedbackTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(128)]
    public async Task ArbitraryConfiguredRoutesHaveIndependentSequenceAndFreshness(int count)
    {
        var fake = new FakeAdapter(); var connection = new CanConnectionResource(fake);
        try
        {
            var routes = Enumerable.Range(0, count).Select(i => new FeedbackRoute($"Feedback{i}", (uint)(0x180 + i), 1, 2000)).ToArray();
            var service = connection.StartService("configurable", routes);
            foreach (var route in routes)
                fake.Incoming.Add(new() { Id = route.CobId, Data = [0], TimestampNs = 100, TimestampDomain = "test", ClockId = "test",
                    CaptureMonotonicLowerBoundTicks = Stopwatch.GetTimestamp() });
            Assert.True(SpinWait.SpinUntil(() => service.Snapshot().Receive.Frames.All(f => f.Sequence == 1), 2000));
            var snapshot = service.Snapshot().Receive;
            Assert.Equal(count, snapshot.Frames.Length); Assert.True(snapshot.AllValid);
            fake.Incoming.Add(new() { Id = routes[0].CobId, Data = [0], TimestampNs = 101, TimestampDomain = "test", ClockId = "test",
                CaptureMonotonicLowerBoundTicks = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 3 });
            Assert.True(SpinWait.SpinUntil(() => service.Snapshot().Receive.Frames[0].Sequence == 2, 2000));
            var next = service.Snapshot().Receive.Frames;
            Assert.Equal("Stale", next[0].Quality);
            Assert.All(next.Skip(1), frame => { Assert.Equal(1, frame.Sequence); Assert.True(frame.Valid); });
        }
        finally { await connection.CloseAsync(); }
    }

    [Fact]
    public async Task MoreThan128RoutesIsRejected()
    {
        var connection = new CanConnectionResource(new FakeAdapter());
        try
        {
            Assert.Throws<ArgumentException>(() => connection.StartService("too many",
                Enumerable.Range(0, 129).Select(i => new FeedbackRoute($"p{i}", (uint)i, 1, 100)).ToArray()));
        }
        finally { await connection.CloseAsync(); }
    }
}
