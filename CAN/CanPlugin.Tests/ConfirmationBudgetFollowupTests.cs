using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using CAN.Adapters;
using CAN.Adapters.NiXnet;
using CAN.Adapters.Zlg;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class ConfirmationBudgetFollowupTests
{
    [Theory]
    [InlineData("NI", "cycle")]
    [InlineData("ZLG", "cycle")]
    [InlineData("NI", "monitor")]
    [InlineData("ZLG", "monitor")]
    [InlineData("NI", "stop")]
    [InlineData("ZLG", "stop")]
    [InlineData("NI", "public-default")]
    [InlineData("ZLG", "public-default")]
    public async Task Configured_confirmation_budget_reaches_the_driver_while_public_calls_keep_the_default(string vendor, string mode)
    {
        using var guard = new CancellationTokenSource(25000);
        uint delayedId = mode == "monitor" ? 0x80u : mode == "stop" ? 0x601u : 0x201u;
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ICanAdapter adapter; Action emit;
        if (vendor == "NI")
        {
            var driver = new BudgetNiDriver(delayedId, written);
            adapter = new NiXnetAdapter(driver, 1, 2); emit = driver.EmitDelayed;
        }
        else
        {
            var driver = new CAN.ContractTests.ZlgRuntimeTests.Driver { AutoEcho = false };
            ZlgApi.ZCAN_Receive_Data delayed = default;
            driver.OnClassicTransmit = frame =>
            {
                if (frame.frame.can_id == delayedId) { delayed = driver.LastClassic; written.TrySetResult(); }
                else driver.Classic.Enqueue(driver.LastClassic);
            };
            adapter = new ZlgAdapter(driver, 41, false);
            emit = () =>
            {
                driver.Classic.Enqueue(delayed);
                if (delayedId == 0x601)
                    driver.Classic.Enqueue(new() { timestamp = delayed.timestamp + 1, frame = new() { can_id = 0x581, can_dlc = 8,
                        data = [0x60, delayed.frame.data![1], delayed.frame.data[2], delayed.frame.data[3], 0, 0, 0, 0] } });
            };
        }
        CanConnectionResource? connection = null;
        Task? publicWrite = null, stopping = null;
        try
        {
            if (mode == "public-default") publicWrite = Task.Run(() => ((ICanConfirmedTransmitter)adapter).WriteConfirmed(new() { Id = delayedId, Data = [1] }, default));
            else
            {
                connection = new CanConnectionResource(adapter);
                var service = connection.StartService("budget", [new("one", 0x181, 1, 100)]);
                if (mode == "monitor") service.StartMonitoring(new(0x80, [0]), new(10000, 8000, 1000));
                else
                {
                    service.StartConfiguredTargets([1], [new(0x201, [1])], new(0x80, [0]),
                        new(10000, 8000, 1000, true, 20000),
                        mode == "stop" ? new([new(1, 0x2000, 0, [1])], 8000) : null);
                    if (mode == "stop")
                    {
                        await WaitCycle(service, guard.Token);
                        stopping = service.StopTargetsAsync(12000, guard.Token);
                    }
                }
                await written.Task.WaitAsync(guard.Token);
                // Real wall time, not a fake clock or configured-counter rewrite:
                // the echo arrives after the old internal five-second cap.
                await Task.Delay(5500, guard.Token); emit();
                if (mode == "stop")
                {
                    await stopping!.WaitAsync(guard.Token);
                    Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
                }
                else { await WaitCycle(service, guard.Token); Assert.Null(service.Snapshot().Cycle.Error); }
                await service.StopTargetsAsync(8000, guard.Token);
                return;
            }
            await written.Task.WaitAsync(guard.Token);
            await Task.Delay(5500, guard.Token); emit();
            await Assert.ThrowsAsync<TimeoutException>(() => publicWrite!.WaitAsync(guard.Token));
        }
        finally
        {
            if (written.Task.IsCompleted) emit();
            if (publicWrite != null) try { await publicWrite; } catch { }
            if (stopping != null) try { await stopping; } catch { }
            if (connection != null)
            {
                try { await connection.CloseAsync(8000, guard.Token); }
                catch (Exception) when (connection.IsClosed) { }
            }
            else
            {
                // Late echoes continue to be consumed after a public wait times out.
                while (!((ICanTransmitQuiescence)adapter).ConfirmTransmitQuiescent()) await Task.Delay(1, guard.Token);
                adapter.Close();
            }
        }
    }

    private static async Task WaitCycle(CanopenCyclicService service, CancellationToken ct)
    {
        while (service.Snapshot().Cycle.CompletedCycles == 0 && service.Snapshot().Cycle.Error == null) await Task.Delay(1, ct);
        Assert.Null(service.Snapshot().Cycle.Error);
        Assert.True(service.Snapshot().Cycle.CompletedCycles > 0);
    }

    internal sealed class BudgetNiDriver(uint delayedId, TaskCompletionSource written) : NiXnetIo
    {
        internal int Writes;
        private readonly ConcurrentQueue<byte[]> _incoming = new();
        private byte[]? _delayed;
        private byte[][] _following = [];
        public override int WriteBatch(uint session, byte[] buffer, double timeoutSeconds)
        {
            var frames = NiOrderedBatchTests.BatchDriver.Split(buffer).ToArray();
            int delayed = Array.FindIndex(frames, frame => BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8)) == delayedId);
            if (delayed >= 0) _following = frames.Skip(delayed + 1).ToArray();
            for (int i = 0; i < frames.Length; i++)
            {
                Interlocked.Increment(ref Writes);
                if (i == delayed) { _delayed = frames[i]; written.TrySetResult(); }
                else if (delayed < 0 || i < delayed) Echo(frames[i]);
            }
            return 0;
        }
        private static ulong Now => 130000000000000000UL + (ulong)(Stopwatch.GetTimestamp() * (10_000_000m / Stopwatch.Frequency));
        public override int Read(uint session, byte[] buffer, out uint count)
        { if (_incoming.TryDequeue(out var frame)) { frame.CopyTo(buffer, 0); count = (uint)frame.Length; } else count = 0; return 0; }
        public override int Write(uint session, byte[] buffer)
        {
            Interlocked.Increment(ref Writes);
            var frame = buffer.ToArray();
            if (BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8)) == delayedId) { _delayed = frame; written.TrySetResult(); }
            else Echo(frame);
            return 0;
        }
        public void EmitDelayed()
        {
            var frame = _delayed!; Echo(frame);
            foreach (var following in _following) Echo(following);
            if (delayedId == 0x601)
            {
                var response = new byte[24]; BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(8), 0x581);
                BinaryPrimitives.WriteUInt64LittleEndian(response, Now); response[15] = 8;
                response[16] = 0x60; Array.Copy(frame, 17, response, 17, 3); _incoming.Enqueue(response);
            }
        }
        private void Echo(byte[] source)
        { var frame = source.ToArray(); frame[13] = 0x80; BinaryPrimitives.WriteUInt64LittleEndian(frame, Now); _incoming.Enqueue(frame); }
        public override int ReadTime(uint session, out ulong time, out int fault) { time = Now; fault = 0; return 0; }
        public override int TransmitComplete(uint session) => 0;
        public override int Stop(uint session) => 0;
        public override int State(uint session, out uint state, out int fault) { state = 0; fault = 0; return 0; }
        public override int Clear(uint session) => 0;
        public override void Check(int status) { if (status < 0) throw new InvalidOperationException($"fake NI {status}"); }
    }
}
