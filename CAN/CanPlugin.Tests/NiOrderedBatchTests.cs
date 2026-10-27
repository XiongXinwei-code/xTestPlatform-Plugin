using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using CAN.Adapters.NiXnet;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class NiOrderedBatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_native_batch_retains_buffers_and_owner_until_real_exit_including_late_failure(bool lateFailure)
    {
        var io = new NiBatchEvidenceTests.Driver { BlockNative = true };
        var owner = new CanConnectionResource(new NiXnetAdapter(io, 1, 2));
        var service = owner.StartService("batch", [new("feedback", 0x181, 1, 1000)]);
        byte[] target = [1, 2, 3, 4];
        try
        {
            service.StartTargets([1], [new(0x201, target)], new(0x80, [0]), new(10000, 5000, 1000, true, 20000));
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); target[0] = 99;
            Assert.Equal(1, io.Buffer![16]);
            Assert.Equal(new uint[] { 0x201, 0x80 }, io.Ids); // Already entered the SDK as one complete buffer.
            await Assert.ThrowsAsync<TimeoutException>(() => service.StopTargetsAsync(30));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            Assert.False(owner.IsClosed); Assert.Equal(0, io.Cleared); Assert.False(service.Snapshot().Cycle.HostExited);
            if (lateFailure) io.ThrowAfterNative = "fake late native batch failure";
            io.Release.Set();
            try { await owner.CloseAsync(5000); } catch (Exception) when (owner.IsClosed) { }
            Assert.True(owner.IsClosed); Assert.Equal(2, io.Cleared); Assert.Equal(1, io.Calls);
            Assert.False(service.Snapshot().Cycle.StopCommandsAcknowledged); Assert.False(service.Snapshot().Cycle.MotorStoppedVerified);
            var diagnostic = service.Snapshot().Cycle.Timing!.Batch!;
            Assert.True(diagnostic.NativeExited);
            if (lateFailure)
            {
                Assert.Contains("fake late native batch failure", service.Snapshot().Cycle.Error);
                Assert.Contains("fake late native batch failure", diagnostic.Error);
                Assert.All(diagnostic.Frames, frame => Assert.False(frame.NativeSucceeded));
            }
        }
        finally { io.Release.Set(); try { await owner.CloseAsync(5000); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task Concurrent_foreign_write_cannot_split_the_owned_batch()
    {
        var io = new NiBatchEvidenceTests.Driver { BlockNative = true };
        var owner = new CanConnectionResource(new NiXnetAdapter(io, 1, 2));
        var service = owner.StartService("batch", [new("feedback", 0x181, 1, 1000)]);
        Task? foreign = null;
        try
        {
            service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(10000, 5000, 1000, true, 20000));
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            foreign = owner.WriteAsync(new() { Id = 0x701, Data = [7] }, 5000);
            Assert.False(foreign.IsCompleted); Assert.Equal(0, io.SingleCalls);
            io.Release.Set(); await foreign.WaitAsync(TimeSpan.FromSeconds(5));
            using var guard = new CancellationTokenSource(5000);
            while (service.Snapshot().Cycle.CompletedCycles == 0) await Task.Delay(2, guard.Token);
            Assert.Equal(new uint[] { 0x201, 0x80, 0x701 }, io.Ids);
            var batch = service.Snapshot().Cycle.Timing!.Batch!;
            Assert.Equal("NiXnetOrderedRawBatch", batch.Strategy); Assert.Equal(owner.Epoch, batch.ConnectionEpoch);
            Assert.NotNull(batch.MotionGeneration); Assert.Equal(1, batch.NativeCallCount);
            batch.Frames[0] = null!;
            Assert.NotNull(service.Snapshot().Cycle.Timing!.Batch!.Frames[0]); // A consumer cannot mutate retained evidence.
            await service.StopTargetsAsync();
        }
        finally { io.Release.Set(); if (foreign != null) try { await foreign; } catch (Exception) { } try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task Legacy_adapters_are_explicitly_labelled_and_single_Sync_is_compatible_without_batch_order_capability()
    {
        var legacy = new CanConnectionResource(new ConfirmedAdapter());
        var service = legacy.StartService("legacy", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(10000, 5000, 1000, true, 20000));
            using var guard = new CancellationTokenSource(5000);
            while (service.Snapshot().Cycle.CompletedCycles == 0) await Task.Delay(2, guard.Token);
            Assert.Equal("SequentialConfirmedCompatibility", service.Snapshot().Cycle.Timing!.Batch!.Strategy);
            Assert.Null(service.Snapshot().Cycle.Timing!.Batch!.NativeCallCount);
        }
        finally { await legacy.CloseAsync(); }
        var io = new NiBatchEvidenceTests.Driver(); var owner = new CanConnectionResource(new NiXnetAdapter(io, 1, 2, false));
        service = owner.StartService("single", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 5000, 1000));
            using var guard = new CancellationTokenSource(5000);
            while (service.Snapshot().Cycle.CompletedCycles == 0) await Task.Delay(2, guard.Token);
            Assert.Throws<NotSupportedException>(() => service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new()));
            Assert.Null(service.Snapshot().Cycle.Error); Assert.All(io.Ids, id => Assert.Equal(0x80u, id));
            Assert.Equal(0, io.Calls); Assert.True(io.SingleCalls > 0);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Rpdo_echo_wait_does_not_block_submission_of_the_associated_Sync()
    {
        var io = new BatchDriver();
        var adapter = new NiXnetAdapter(io, 1, 2);
        var owner = new CanConnectionResource(adapter);
        var service = owner.StartService("batch", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartTargets([1], [new(0x201, [1, 2, 3, 4])], new(0x80, [0]), new(1000, 2000, 1000));
            await io.SyncSubmitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var guard = new CancellationTokenSource(5000);
            while (service.Snapshot().Cycle.CompletedCycles == 0 && service.Snapshot().Cycle.Error == null)
                await Task.Delay(2, guard.Token);
            Assert.Null(service.Snapshot().Cycle.Error);
            Assert.Equal(new uint[] { 0x201, 0x80 }, io.Ids.Take(2));
            Assert.True(service.Snapshot().Cycle.CompletedCycles > 0);
        }
        finally { try { await owner.CloseAsync(5000); } catch (Exception) when (owner.IsClosed) { } }
    }

    internal sealed class BatchDriver : NiXnetIo
    {
        internal readonly ConcurrentQueue<uint> Ids = new();
        internal readonly TaskCompletionSource SyncSubmitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<byte[]> _incoming = new();
        private readonly List<byte[]> _held = new();
        private static ulong Now => 130000000000000000UL + (ulong)(Stopwatch.GetTimestamp() * (10_000_000m / Stopwatch.Frequency));
        public override int WriteBatch(uint session, byte[] buffer, double timeoutSeconds) => Write(session, buffer);
        public override int Write(uint session, byte[] buffer)
        {
            foreach (var frame in Split(buffer))
            {
                var id = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8)); Ids.Enqueue(id); _held.Add(frame);
                if (id != 0x80) continue;
                SyncSubmitted.TrySetResult();
                foreach (var echo in _held)
                { echo[13] |= 0x80; BinaryPrimitives.WriteUInt64LittleEndian(echo, Now); _incoming.Enqueue(echo); }
                _held.Clear();
            }
            return 0;
        }
        internal static IEnumerable<byte[]> Split(byte[] buffer)
        {
            for (int offset = 0; offset < buffer.Length;)
            {
                int size = 16 + Math.Max(8, (buffer[offset + 15] + 7) & ~7);
                yield return buffer.AsSpan(offset, size).ToArray(); offset += size;
            }
        }
        public override int Read(uint session, byte[] buffer, out uint count)
        { if (_incoming.TryDequeue(out var frame)) { frame.CopyTo(buffer, 0); count = (uint)frame.Length; } else count = 0; return 0; }
        public override int ReadTime(uint session, out ulong time, out int fault) { time = Now; fault = 0; return 0; }
        public override int TransmitComplete(uint session) => 0;
        public override int Stop(uint session) => 0;
        public override int Clear(uint session) => 0;
        public override void Check(int status) { if (status < 0) throw new InvalidOperationException("fake NI status " + status); }
    }
}
