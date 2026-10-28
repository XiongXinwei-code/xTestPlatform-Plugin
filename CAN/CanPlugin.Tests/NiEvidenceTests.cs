using CAN.Adapters.NiXnet;
using CAN.Models;
using Xunit;

namespace CAN.ContractTests;

public class NiEvidenceTests
{
    private static CanMessage Frame(ulong time, byte value = 1) => new()
    { Id = 0x201, Data = [value], RawTimestamp100ns = time, IsTransmitEcho = true };

    [Fact]
    public void EchoRequiresEpochBarrierPayloadAndNativeExit()
    {
        var ledger = new NiTransmitLedger();
        var epoch = Guid.NewGuid();
        ledger.Reset(epoch);
        var ticket = ledger.Register(Frame(0), 100);
        ledger.Observe(epoch, Frame(99));
        ledger.Observe(Guid.NewGuid(), Frame(101));
        ledger.Observe(epoch, Frame(101, 2));
        Assert.False(ticket.EchoSeen);
        ledger.Observe(epoch, Frame(102));
        Assert.True(ticket.EchoSeen);
        Assert.False(ledger.Quiescent);
        ledger.NativeExited(ticket, true);
        Assert.False(ledger.Quiescent);
        ledger.SessionTransmitted();
        Assert.True(ledger.Quiescent);
    }

    [Fact]
    public void CancelledWaitDoesNotDeletePendingOrAllowDuplicateEchoReuse()
    {
        var ledger = new NiTransmitLedger();
        var epoch = Guid.NewGuid(); ledger.Reset(epoch);
        var first = ledger.Register(Frame(0), 100);
        ledger.NativeExited(first, true);
        Assert.False(ledger.Quiescent); // No waiter owns the lifetime of a ticket.
        ledger.Observe(epoch, Frame(101)); ledger.SessionTransmitted();
        var second = ledger.Register(Frame(0), 102);
        ledger.NativeExited(second, true);
        ledger.Observe(epoch, Frame(101)); ledger.SessionTransmitted();
        Assert.False(second.Confirmed);
        ledger.Observe(epoch, Frame(103)); ledger.SessionTransmitted();
        Assert.True(second.Confirmed);
    }

    [Fact]
    public void NativeFailureCannotBeRepairedByEchoOrSessionComplete()
    {
        var ledger = new NiTransmitLedger(); var epoch = Guid.NewGuid(); ledger.Reset(epoch);
        var ticket = ledger.Register(Frame(0), 100);
        ledger.Observe(epoch, Frame(101)); ledger.NativeExited(ticket, false); ledger.SessionTransmitted();
        Assert.False(ledger.Quiescent);
    }

    [Fact]
    public void ClockUsesEarlierCalibrationBoundNotDequeueTime()
    {
        var clock = new NiInterfaceClock(1000);
        clock.Add(1_000_000, 100, 102);
        clock.Add(1_100_000, 110, 112);
        Assert.Equal(100, clock.LowerBound(1_050_000, 112));
        Assert.Null(clock.LowerBound(999_999, 112));
        Assert.Null(clock.LowerBound(1_050_000, 500));
        var epoch = clock.ClockId;
        clock.Add(100, 120, 122);
        Assert.NotEqual(epoch, clock.ClockId);
        Assert.Null(clock.LowerBound(1_050_000, 122));
    }
}
