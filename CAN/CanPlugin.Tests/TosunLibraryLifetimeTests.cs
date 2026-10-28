using System.Reflection;
using System.Runtime.InteropServices;
using CAN.Adapters;
using CAN.Adapters.Tosun;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class TosunLibraryLifetimeTests
{
    [Fact]
    public void InitializationAbiUsesThreeExplicitOneByteBooleans()
    {
        var args = typeof(TosunApi).GetMethod(nameof(TosunApi.InitializeLib))!.GetParameters();
        Assert.Equal(3, args.Length);
        Assert.All(args, p => Assert.Equal(UnmanagedType.I1, p.GetCustomAttribute<MarshalAsAttribute>()!.Value));
    }

    [Fact]
    public void ParallelAdapterRejectedBeforeInitializingAndLosingDisposeCannotFinalizeOwner()
    {
        var firstIo = new FakeIo(); var nextIo = new FakeIo();
        using var first = new TosunAdapter(firstIo); using var next = new TosunAdapter(nextIo);
        first.Open(Config());
        Assert.Throws<InvalidOperationException>(() => next.Open(Config()));
        Assert.Empty(nextIo.Events);
        next.Dispose(); Assert.True(first.IsConnected); Assert.Equal(0, firstIo.Finalizes);
        first.Close(); next.Open(Config()); Assert.Equal(1, nextIo.Initializes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnconfirmedCloseRetainsGlobalLeaseUntilSuccessfulRetry(bool finalize)
    {
        var io = new FakeIo(); var nextIo = new FakeIo();
        var first = new TosunAdapter(io); var next = new TosunAdapter(nextIo);
        try
        {
            first.Open(Config()); io.FailDisconnect = !finalize; io.FailFinalize = finalize;
            Assert.Throws<InvalidOperationException>(first.Close);
            Assert.False(first.HandlesReleased);
            Assert.Throws<InvalidOperationException>(() => next.Open(Config()));
            Assert.Empty(nextIo.Events);
            io.FailDisconnect = io.FailFinalize = false;
            first.Close(); Assert.True(first.HandlesReleased);
            next.Open(Config()); Assert.True(next.IsConnected);
            first.Dispose(); Assert.True(next.IsConnected); Assert.Equal(0, nextIo.Finalizes);
        }
        finally { io.FailDisconnect = io.FailFinalize = false; first.Dispose(); next.Dispose(); }
    }

    [Fact]
    public void ConfigurationFailureDisconnectsSuccessfullyOwnedHandleBeforeFinalizing()
    {
        var io = new FakeIo { ConfigureStatus = 92u };
        using var adapter = new TosunAdapter(io);
        Assert.Throws<InvalidOperationException>(() => adapter.Open(Config()));
        Assert.Equal(new[] { "initialize", "connect", "configure", "disconnect", "finalize" }, io.Events);
        Assert.True(adapter.HandlesReleased);
        Assert.Equal((true, false, false), io.Initialization);
    }

    [Fact]
    public void FailedConnectWithoutHandleCanFinalizeOnlyItsOwnInitialization()
    {
        var io = new FakeIo { ConnectStatus = 91, ConnectHandle = 0 };
        using var adapter = new TosunAdapter(io);
        Assert.Throws<InvalidOperationException>(() => adapter.Open(Config()));
        Assert.Equal(new[] { "initialize", "connect", "finalize" }, io.Events);
        Assert.True(adapter.HandlesReleased);
    }

    [Theory]
    [InlineData(5u, 123u)]
    [InlineData(5u, 0u)]
    [InlineData(91u, 123u)]
    public void ExistingOrUnknownHandleMustNeverBeDisconnectedOrFinalized(uint status, uint handle)
    {
        var io = new FakeIo { ConnectStatus = status, ConnectHandle = handle };
        var adapter = new TosunAdapter(io); var nextIo = new FakeIo(); var next = new TosunAdapter(nextIo);
        try
        {
            var error = Record.Exception(() => adapter.Open(Config()));
            Assert.NotNull(error);
            Assert.Contains("重启宿主", error.Message);
            Assert.Equal(new[] { "initialize", "connect" }, io.Events);
            Assert.False(adapter.HandlesReleased);
            Assert.Throws<InvalidOperationException>(adapter.Close);
            Assert.Equal(new[] { "initialize", "connect" }, io.Events);
            Assert.Throws<InvalidOperationException>(() => next.Open(Config()));
            Assert.Empty(nextIo.Events);
        }
        finally
        {
            // This intentionally terminal isolation requires host restart in production.
            // The test owns only fake handles: clear only this exact fake owner between cases,
            // never expose a production reset API or call Disconnect/Finalize on unknown handles.
            var owner = typeof(TosunAdapter).GetField("_libraryOwner", BindingFlags.NonPublic | BindingFlags.Static)!;
            if (ReferenceEquals(owner.GetValue(null), adapter)) owner.SetValue(null, null);
        }
    }

    [Fact]
    public void PartialOpenAndFailedDisconnectPreserveBothErrorsAndRemainRetryable()
    {
        var io = new FakeIo { ConfigureStatus = 92, FailDisconnect = true };
        var adapter = new TosunAdapter(io);
        try
        {
            var error = Assert.Throws<AggregateException>(() => adapter.Open(Config()));
            Assert.Contains("92", error.ToString()); Assert.Contains("93", error.ToString());
            Assert.False(adapter.HandlesReleased); Assert.Equal(0, io.Finalizes);
            io.FailDisconnect = false; adapter.Close(); Assert.True(adapter.HandlesReleased);
        }
        finally { io.FailDisconnect = false; adapter.Dispose(); }
    }

    private static CanAdapterConfig Config() => new() { Channel = "0" };
    private sealed class FakeIo : TosunIo
    {
        internal readonly List<string> Events = [];
        internal int Initializes, Finalizes;
        internal uint ConnectStatus, ConfigureStatus;
        internal nuint ConnectHandle = 123;
        internal bool FailDisconnect, FailFinalize;
        internal (bool, bool, bool) Initialization;
        public override void Initialize(bool fifo, bool receiveErrors, bool useConnectionTime) { Initializes++; Initialization = (fifo, receiveErrors, useConnectionTime); Events.Add("initialize"); }
        public override uint Connect(string serial, ref nuint handle) { Events.Add("connect"); handle = ConnectHandle; return ConnectStatus; }
        public override uint ConfigureCan(nuint handle, int channel, double baudKbps, uint termination) { Events.Add("configure"); return ConfigureStatus; }
        public override uint ConfigureFd(nuint handle, int channel, double baudKbps, double dataKbps, uint termination) => ConfigureCan(handle, channel, baudKbps, termination);
        public override uint Disconnect(nuint handle) { Events.Add("disconnect"); return FailDisconnect ? 93u : 0; }
        public override void FinalizeLibrary() { Events.Add("finalize"); if (FailFinalize) throw new InvalidOperationException("finalize failed"); Finalizes++; }
    }
}
