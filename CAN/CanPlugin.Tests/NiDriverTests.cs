using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using CAN.Adapters.NiXnet;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.ContractTests;

[Collection("CAN blocked I/O")]
public class NiDriverTests
{
    [Fact]
    public async Task BlockedNativePreflightHasBoundedCallerButRetainsHandlesUntilActualExit()
    {
        var driver = new Driver { BlockWait = true };
        var adapter = new NiXnetAdapter(driver, 1, 2); var connection = new CanConnectionResource(adapter);
        var service = connection.StartService("test", [new("one", 0x181, 1, 100)]);
        try
        {
            // 先观察真实 fake native 入口，再验证调用方期限；不要假设 30 ms 内线程池必定已调度。
            var start = Task.Run(() => service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(500, 2000, 1000)));
            Assert.True(driver.Waiting.Wait(5000), "预检必须实际进入 fake native 等待");
            Assert.False(start.IsCompleted, "已进入 native 后调用方仍应受预检期限约束");
            await Assert.ThrowsAsync<TimeoutException>(() => start.WaitAsync(TimeSpan.FromSeconds(5)));
            await Assert.ThrowsAsync<TimeoutException>(() => connection.CloseAsync(30));
            Assert.Empty(driver.Cleared);
        }
        finally { driver.ReleaseWait.Set(); }
        await connection.CloseAsync(2000); Assert.True(connection.IsClosed);
    }
    [Fact]
    public async Task LateEchoCanReleaseIsolatedConnectionWithoutReplayingTargets()
    {
        var driver = new Driver { EmitEcho = false };
        var adapter = new NiXnetAdapter(driver, 1, 2); var key = Guid.NewGuid().ToString();
        var connection = new CanConnectionResource(adapter, key);
        var service = connection.StartService("test", [new("one", 0x181, 1, 100)]);
        service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(500, 200, 1000));
        Assert.True(driver.Written.Wait(2000));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StopTargetsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CloseAsync());
        Assert.Empty(driver.Cleared);
        driver.EmitLast(); await Task.Delay(30);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CloseAsync());
        Assert.True(connection.IsClosed); Assert.Equal(2, driver.Cleared.Count);
        var next = new CanConnectionResource(new CAN.Tests.FakeAdapter(), key); await next.CloseAsync();
    }

    [Fact]
    public async Task HistoricalPumpErrorDoesNotKeepReleasedPhysicalChannelReserved()
    {
        var driver = new Driver { ReadError = -1 };
        var adapter = new NiXnetAdapter(driver, 1, 2); var key = Guid.NewGuid().ToString();
        var connection = new CanConnectionResource(adapter, key);
        Assert.True(driver.Reading.Wait(2000)); await Task.Delay(30);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CloseAsync());
        Assert.True(connection.IsClosed);
        var next = new CanConnectionResource(new CAN.Tests.FakeAdapter(), key); await next.CloseAsync();
    }

    [Fact]
    public void OrdinaryWriteDoesNotDependOnUnavailableClockConfirmation()
    {
        var driver = new Driver { TimeError = -1 };
        var adapter = new NiXnetAdapter(driver, 1, 2);
        adapter.Write(new() { Id = 0x601, Data = [1] });
        Assert.True(driver.Written.IsSet);
        Assert.Throws<InvalidOperationException>(adapter.VerifyConfirmationAvailable);
        adapter.Close();
    }
    [Fact]
    public async Task EchoBeforeWriteReturnDoesNotCompleteWrite()
    {
        var driver = new Driver { BlockWrite = true };
        var adapter = new NiXnetAdapter(driver, 1, 2);
        var write = Task.Run(() => adapter.WriteConfirmed(new CanMessage { Id = 0x201, Data = [1] }, default));
        Assert.True(driver.Written.Wait(2000));
        await Task.Delay(30); Assert.False(write.IsCompleted);
        driver.ReleaseWrite.Set(); await write.WaitAsync(TimeSpan.FromSeconds(2));
        adapter.Close(); Assert.Equal(2, driver.Cleared.Count);
    }

    [Fact]
    public async Task MissingConfirmationRetainedAfterCancellationAndClosedOnlyAfterLateEcho()
    {
        var driver = new Driver { EmitEcho = false };
        var adapter = new NiXnetAdapter(driver, 1, 2);
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() =>
            adapter.WriteConfirmed(new CanMessage { Id = 0x201, Data = [1] }, cts.Token)));
        Assert.Throws<InvalidOperationException>(() => adapter.Write(new() { Id = 0x201, Data = [2] }));
        Assert.Throws<TimeoutException>(adapter.Close);
        Assert.Empty(driver.Cleared);
        driver.EmitLast(); await Task.Delay(30);
        adapter.Close(); Assert.Equal(2, driver.Cleared.Count);
    }

    [Fact]
    public async Task ReceivePumpStillRunningPreventsHandleDestruction()
    {
        var driver = new Driver { BlockRead = true };
        var adapter = new NiXnetAdapter(driver, 1, 2);
        Assert.True(driver.Reading.Wait(2000));
        Assert.Throws<TimeoutException>(adapter.Close); Assert.Empty(driver.Cleared);
        driver.ReleaseRead.Set(); await Task.Delay(30);
        adapter.Close(); Assert.Equal(2, driver.Cleared.Count);
    }

    [Fact]
    public void ClearFailureRetainsHandleForRetry()
    {
        var driver = new Driver { FailClear = true };
        var adapter = new NiXnetAdapter(driver, 1, 2);
        Assert.Throws<InvalidOperationException>(adapter.Close);
        Assert.Empty(driver.Cleared);
        driver.FailClear = false; adapter.Close(); Assert.Equal(2, driver.Cleared.Count);
    }

    private sealed class Driver : NiXnetIo
    {
        private readonly ConcurrentQueue<byte[]> _echoes = new();
        public readonly ConcurrentBag<uint> Cleared = [];
        public readonly ManualResetEventSlim Written = new(), ReleaseWrite = new(), Reading = new(), ReleaseRead = new();
        public readonly ManualResetEventSlim Waiting = new(), ReleaseWait = new();
        public bool BlockWrite, BlockRead, FailClear, EmitEcho = true;
        public bool BlockWait;
        public int ReadError, TimeError;
        private byte[]? _last;
        private static ulong Now => 130000000000000000UL + (ulong)(Stopwatch.GetTimestamp() * (10_000_000m / Stopwatch.Frequency));
        public override int Read(uint session, byte[] buffer, out uint count)
        {
            Reading.Set(); if (BlockRead) ReleaseRead.Wait();
            if (_echoes.TryDequeue(out var data)) { data.CopyTo(buffer, 0); count = (uint)data.Length; }
            else count = 0;
            return ReadError;
        }
        public void EmitLast()
        {
            var echo = _last!.ToArray(); echo[13] = 0x80;
            BinaryPrimitives.WriteUInt64LittleEndian(echo, Now); _echoes.Enqueue(echo);
        }
        public override int Write(uint session, byte[] buffer)
        {
            _last = buffer.ToArray(); if (EmitEcho) EmitLast(); Written.Set();
            if (BlockWrite) ReleaseWrite.Wait(); return 0;
        }
        public override int ReadTime(uint session, out ulong time, out int fault) { time = Now; fault = 0; return TimeError; }
        public override int TransmitComplete(uint session)
        {
            Waiting.Set(); if (BlockWait) ReleaseWait.Wait();
            return Written.IsSet && BlockWrite && !ReleaseWrite.IsSet ? NiXnetApi.nxErrEventTimeout : 0;
        }
        public override int Stop(uint session) => 0;
        public override int State(uint session, out uint value, out int fault) { value = 0; fault = 0; return 0; }
        public override int Clear(uint session) { if (FailClear) return -1; Cleared.Add(session); return 0; }
        public override void Check(int status) { if (status < 0) throw new InvalidOperationException($"fake NI: {status}"); }
    }
}
