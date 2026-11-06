using LabVIEWCallPlugin.BridgeProtocol;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class BridgeReferenceSlotTests
{
    [Fact]
    public void NativeCookieReuseAcrossLoadsAndPortsNeverReusesTheOpaqueTicket()
    {
        var slot = new BridgeReferenceSlot();
        uint a = slot.Load(3363, () => 0x777);
        Assert.Equal(0x777u, slot.Resolve(a, 3363));
        uint b = slot.Load(3364, () => 0x777);
        Assert.NotEqual(a, b);
        Assert.Throws<InvalidOperationException>(() => slot.Resolve(a, 3363));
        Assert.Throws<InvalidOperationException>(() => slot.Resolve(a, 3364));
        Assert.Throws<InvalidOperationException>(() => slot.Resolve(b, 3363));
        Assert.Equal(0x777u, slot.Resolve(b, 3364));
        uint samePortAgain = slot.Load(3364, () => 0x777);
        Assert.NotEqual(b, samePortAgain);
        Assert.Throws<InvalidOperationException>(() => slot.Resolve(b, 3364));
    }

    [Fact]
    public void FailedLoadInvalidatesOldReferenceAndDoesNotPublishANewTicket()
    {
        var slot = new BridgeReferenceSlot();
        uint a = slot.Load(3363, () => 7);
        Assert.Throws<TimeoutException>(() => slot.Load(3364, () => throw new TimeoutException()));
        Assert.Throws<InvalidOperationException>(() => slot.Resolve(a, 3363));
        Assert.Throws<InvalidOperationException>(() => slot.Resolve(a + 1, 3364));
        uint next = slot.Load(3363, () => 7);
        Assert.Equal(a + 2, next);
    }

    [Fact]
    public void RunPreservesTicketAndUpdatesNativeCookieButFailedRunInvalidatesIt()
    {
        var slot = new BridgeReferenceSlot();
        uint ticket = slot.Load(3363, () => 7);
        Assert.Equal(ticket, slot.Run(ticket, 3363, native => native + 1));
        Assert.Equal(8u, slot.Resolve(ticket, 3363));
        Assert.Throws<TimeoutException>(() => slot.Run(ticket, 3363, _ => throw new TimeoutException()));
        Assert.Throws<InvalidOperationException>(() => slot.Resolve(ticket, 3363));
    }

    [Fact]
    public void WrongPortAndTicketNeverReachNativeRun()
    {
        var slot = new BridgeReferenceSlot();
        uint ticket = slot.Load(3363, () => 7);
        int calls = 0;
        uint Run(uint value) { calls++; return value; }
        Assert.Throws<InvalidOperationException>(() => slot.Run(ticket, 3364, Run));
        Assert.Throws<InvalidOperationException>(() => slot.Run(ticket + 1, 3363, Run));
        Assert.Equal(0, calls);
        Assert.Equal(7u, slot.Resolve(ticket, 3363));
    }

    [Fact]
    public void TicketExhaustionFailsBeforeNativeLoadAndNeverWraps()
    {
        var slot = new BridgeReferenceSlot(uint.MaxValue - 1);
        int calls = 0;
        uint Load() { calls++; return 7; }
        uint final = slot.Load(3363, Load);
        Assert.Equal(uint.MaxValue, final);
        Assert.Throws<InvalidOperationException>(() => slot.Load(3363, Load));
        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() => slot.Resolve(final, 3363));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void InvalidPortFailsBeforeNativeLoad(int port)
    {
        int calls = 0;
        var slot = new BridgeReferenceSlot();
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.Load(port, () => { calls++; return 7; }));
        Assert.Equal(0, calls);
    }
}
