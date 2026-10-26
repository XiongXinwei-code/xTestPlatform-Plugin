using System.Collections.Concurrent;
using CAN.Adapters.NiXnet;
using CAN.Runtime;
using Xunit;

namespace CAN.ContractTests;

[Collection("CAN blocked I/O")]
public sealed class NiSdkCloseContractTests
{
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    public void Sdk_Clear_can_stop_and_release_own_sessions_without_a_preclear_stopped_probe(uint state)
    {
        var io = new DeferredStopIo { ReportedState = state };
        var adapter = new NiXnetAdapter(io, 41, 42);
        try
        {
            adapter.Close();
            Assert.True(adapter.HandlesReleased);
            Assert.Equal(new[] { "stop:41", "stop:42", "clear:41", "clear:42" }, io.Calls.ToArray());
            Assert.Equal(0, io.StateReads);
            Assert.DoesNotContain(io.Cleared, s => s == 99); // An unrelated session must never be touched.
        }
        finally { io.ReportedState = 0; adapter.Close(); }
    }

    [Fact]
    public void Optional_Stop_error_does_not_prevent_Sdk_Clear_and_still_remains_an_explicit_error()
    {
        var io = new DeferredStopIo { FailStop = 41 };
        var adapter = new NiXnetAdapter(io, 41, 42);
        try
        {
            var failure = Assert.ThrowsAny<Exception>(adapter.Close);
            Assert.Contains("fake NI SDK failure", failure.ToString());
            Assert.True(adapter.HandlesReleased);
            Assert.False(adapter.IsConnected);
            Assert.Equal(new[] { "stop:41", "stop:42", "clear:41", "clear:42" }, io.Calls.ToArray());
        }
        finally { io.FailStop = 0; adapter.Close(); }
    }

    [Fact]
    public void First_Clear_error_does_not_prevent_releasing_the_other_independent_session()
    {
        var io = new DeferredStopIo { FailClear = 41 };
        var adapter = new NiXnetAdapter(io, 41, 42);
        try
        {
            Assert.ThrowsAny<Exception>(adapter.Close);
            Assert.False(adapter.HandlesReleased);
            Assert.Equal(new uint[] { 42 }, io.Cleared.ToArray());
            io.FailClear = 0;
            adapter.Close();
            Assert.True(adapter.HandlesReleased);
            Assert.Equal(1, io.Cleared.Count(s => s == 42));
        }
        finally { io.FailClear = 0; adapter.Close(); }
    }

    [Fact]
    public void Partial_Clear_failure_keeps_only_the_unreleased_handle_for_retry()
    {
        var io = new DeferredStopIo { FailClear = 42 };
        var adapter = new NiXnetAdapter(io, 41, 42);
        try
        {
            Assert.Throws<InvalidOperationException>(adapter.Close);
            Assert.False(adapter.HandlesReleased);
            Assert.Equal(new uint[] { 41 }, io.Cleared.ToArray());
            io.FailClear = 0;
            adapter.Close();
            Assert.True(adapter.HandlesReleased);
            Assert.Equal(1, io.Cleared.Count(s => s == 41));
            Assert.Equal(1, io.Cleared.Count(s => s == 42));
        }
        finally { io.FailClear = 0; adapter.Close(); }
    }

    [Fact]
    public async Task Native_Clear_still_running_keeps_physical_owner_after_foreground_close_times_out()
    {
        var io = new DeferredStopIo { BlockClear = true };
        var adapter = new NiXnetAdapter(io, 41, 42);
        string physical = "NI:close-contract-" + Guid.NewGuid().ToString("N");
        var owner = new CanConnectionResource(adapter, physical);
        var closing = owner.CloseAsync(3000);
        try
        {
            await io.ClearEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            Assert.False(owner.IsClosed); Assert.False(adapter.HandlesReleased); Assert.Empty(io.Cleared);
            Assert.Throws<InvalidOperationException>(() => new CanConnectionResource(new CAN.Tests.FakeAdapter(), physical));
            io.ReleaseClear.Set(); await closing;
            Assert.True(owner.IsClosed); Assert.True(adapter.HandlesReleased);
            var next = new CanConnectionResource(new CAN.Tests.FakeAdapter(), physical); await next.CloseAsync();
        }
        finally { io.ReleaseClear.Set(); await owner.CloseAsync(5000); }
    }

    private sealed class DeferredStopIo : NiXnetIo
    {
        internal readonly ConcurrentQueue<string> Calls = new();
        internal readonly ConcurrentQueue<uint> Cleared = new();
        internal readonly TaskCompletionSource ClearEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim ReleaseClear = new();
        internal uint ReportedState, FailClear, FailStop;
        internal bool BlockClear;
        internal int StateReads, Reading;
        public override int Read(uint session, byte[] buffer, out uint count)
        { Interlocked.Increment(ref Reading); count = 0; Interlocked.Decrement(ref Reading); return 0; }
        public override int ReadTime(uint session, out ulong time, out int fault)
        { time = (ulong)DateTime.UtcNow.Ticks; fault = 0; return 0; }
        public override int TransmitComplete(uint session) => 0;
        public override int Stop(uint session) { Calls.Enqueue("stop:" + session); return session == FailStop ? -1 : 0; }
        public override int State(uint session, out uint state, out int fault)
        { StateReads++; state = ReportedState; fault = 0; return 0; }
        public override int Clear(uint session)
        {
            Assert.Equal(0, Volatile.Read(ref Reading));
            Calls.Enqueue("clear:" + session); ClearEntered.TrySetResult();
            if (BlockClear && !ReleaseClear.Wait(10000)) throw new TimeoutException("test native clear barrier");
            if (session == FailClear) return -1;
            Cleared.Enqueue(session); return 0;
        }
        public override void Check(int status) { if (status < 0) throw new InvalidOperationException("fake NI SDK failure"); }
    }
}
