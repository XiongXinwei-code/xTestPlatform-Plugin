using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ZlgEtherCat.Models;
using ZlgEtherCat.Native;
using ZlgEtherCat.Runtime;
using Xunit;

namespace ZlgEtherCat.Tests;

public sealed class AcquisitionDiagnosticsTests
{
    [Fact]
    public async Task FullQueueIsObservedBeforeFifoDrainAndLatestDiscardIsNotAHardwareLossCount()
    {
        foreach (bool latest in new[] { false, true })
        {
            await using var session = await Session.OpenAsync();
            for (byte i = 1; i <= 5; i++) session.Api.Enqueue(i);
            await session.StartAsync(latest: latest);
            await Eventually(async () => (await session.Status()).GetProperty("Sequence").ValueKind == JsonValueKind.Number);
            var status = await session.Status();
            var rx = status.GetProperty("RxQueue");
            Assert.True(rx.GetProperty("OverflowSuspected").GetBoolean());
            Assert.Equal(4u, rx.GetProperty("PeakDepth").GetUInt32());
            Assert.False(rx.GetProperty("HardwareDroppedFramesAvailable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, rx.GetProperty("HardwareDroppedFrames").ValueKind);
            Assert.Equal(latest ? 1 : 4, status.GetProperty("Sequence").GetInt64());
            var history = JsonSerializer.SerializeToElement(await session.Run("ReadHistory", ("AfterSequence", 0L), ("MaxSamples", 10)));
            Assert.Equal("LocalHistoryEvictions", history.GetProperty("DroppedMeaning").GetString());
            var samples = history.GetProperty("Samples").EnumerateArray().ToArray();
            Assert.Equal(latest ? 1 : 4, samples.Length);
            Assert.All(samples, sample =>
            {
                Assert.Equal("HostMonotonicReadCompletion", sample.GetProperty("TimestampSource").GetString());
                Assert.Equal(Stopwatch.Frequency, sample.GetProperty("TimestampFrequencyHz").GetInt64());
                Assert.False(sample.GetProperty("HardwareTimestampAvailable").GetBoolean());
                Assert.Equal(JsonValueKind.Null, sample.GetProperty("HardwareTimestamp").ValueKind);
            });
            Assert.Single(session.Api.ThreadIds);
        }
    }

    [Fact]
    public async Task RequestedPriorityAppliesToTheExistingWorkerAndStopRestoresItsOriginalPriority()
    {
        await using var session = await Session.OpenAsync();
        await session.StartAsync(AcquisitionPriority.AboveNormal);
        var status = await session.Status();
        Assert.Equal("AboveNormal", status.GetProperty("Priority").GetProperty("Actual").GetString());
        Assert.Equal(ThreadPriority.AboveNormal, await session.Worker.InvokeAsync(() => Thread.CurrentThread.Priority, 1000, default));
        await session.Run("StopAcquisition");
        var stopped = await session.Status();
        Assert.False(stopped.GetProperty("Running").GetBoolean());
        Assert.Equal("Normal", stopped.GetProperty("Priority").GetProperty("Actual").GetString());
        Assert.Equal(1, stopped.GetProperty("WorkerStarts").GetInt32());
    }

    [Fact]
    public async Task FailedRestartDoesNotClearPreviousOverflowEvidenceAndSuccessfulRestartDoes()
    {
        await using var session = await Session.OpenAsync(injectPriority: true);
        for (byte i = 0; i < 4; i++) session.Api.Enqueue(i);
        await session.StartAsync();
        await Eventually(async () => (await session.Status()).GetProperty("RxQueue").GetProperty("OverflowSuspected").GetBoolean());
        await session.Run("StopAcquisition");
        session.FailPriority = ThreadPriority.Highest;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(AcquisitionPriority.Highest));
        var failed = await session.Status();
        Assert.False(failed.GetProperty("Running").GetBoolean());
        Assert.True(failed.GetProperty("RxQueue").GetProperty("OverflowSuspected").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, failed.GetProperty("Priority").GetProperty("Error").ValueKind);
        session.FailPriority = null;
        await session.StartAsync();
        Assert.False((await session.Status()).GetProperty("RxQueue").GetProperty("OverflowSuspected").GetBoolean());
    }

    [Fact]
    public async Task RestoreFailureStopsSamplingButDoesNotPreventNativeClose()
    {
        await using var session = await Session.OpenAsync(injectPriority: true);
        await session.StartAsync(AcquisitionPriority.Highest);
        session.FailPriority = ThreadPriority.Normal;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.Run("StopAcquisition"));
        var status = await session.Status();
        Assert.False(status.GetProperty("Running").GetBoolean());
        Assert.False(status.GetProperty("RxQueue").GetProperty("Monitoring").GetBoolean());
        Assert.Equal("Highest", status.GetProperty("Priority").GetProperty("Actual").GetString());
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("Priority").GetProperty("Error").ValueKind);
        session.ExpectCloseFailure = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.Channel.CloseAsync());
        Assert.Equal(1, session.Api.CloseCalls);
        Assert.Equal(1, session.Releases);
    }

    [Fact]
    public async Task SlowOrFailingWarningDeliveryDoesNotBlockSdk()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int calls = 0;
        await using var session = await Session.OpenAsync(warning: notice =>
        {
            Interlocked.Increment(ref calls);
            entered.Set();
            release.Wait();
            throw new IOException("fake log failure");
        });
        try
        {
            for (byte i = 0; i < 4; i++) session.Api.Enqueue(i);
            await session.StartAsync();
            Assert.True(entered.Wait(2000));
            int oldReads = session.Api.ReadCalls;
            session.Api.Enqueue(9);
            await Eventually(() => Task.FromResult(session.Api.ReadCalls > oldReads));
            Assert.Equal(1, (await session.Status()).GetProperty("WorkerStarts").GetInt32());
            release.Set();
            await Eventually(async () => (await session.Status()).GetProperty("RxQueue").GetProperty("NotificationError").ValueKind == JsonValueKind.String);
            Assert.True((await session.Status()).GetProperty("Running").GetBoolean());
            await session.Channel.CloseAsync();
            int closedCalls = calls;
            session.Api.Enqueue(8);
            Assert.Equal(closedCalls, calls);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task CloseWaitsForEnteredWarningAndOldFailureDoesNotContaminateRestart()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        await using var session = await Session.OpenAsync(warning: _ =>
        {
            entered.Set(); release.Wait(); exited.Set(); throw new IOException("old acquisition warning");
        });
        try
        {
            for (byte i = 0; i < 4; i++) session.Api.Enqueue(i);
            await session.StartAsync();
            Assert.True(entered.Wait(2000));
            await session.Run("StopAcquisition");
            await session.StartAsync();
            release.Set();
            Assert.True(exited.Wait(2000));
            await Task.Delay(20);
            var restarted = (await session.Status()).GetProperty("RxQueue");
            Assert.Equal(JsonValueKind.Null, restarted.GetProperty("NotificationError").ValueKind);
            Assert.False(restarted.GetProperty("OverflowSuspected").GetBoolean());

            entered.Reset(); exited.Reset(); release.Reset();
            await session.Worker.InvokeAsync(() => { for (byte i = 0; i < 4; i++) session.Api.Enqueue(i); }, 1000, default);
            Assert.True(entered.Wait(2000));
            var closing = session.Channel.CloseAsync();
            await Eventually(() => Task.FromResult(session.Api.CloseCalls == 1));
            Assert.False(closing.IsCompleted);
            Assert.Equal(0, session.Releases);
            release.Set();
            await closing.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(exited.IsSet);
            Assert.Equal(1, session.Api.CloseCalls);
            Assert.Equal(1, session.Releases);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task PriorityReadbackMismatchFailsAndRollsBackWithoutCreatingAnotherWorker()
    {
        ThreadPriority actual = ThreadPriority.Normal;
        int writes = 0;
        var worker = new SdkWorker("priority mismatch", null,
            _ => Interlocked.Increment(ref writes), () => actual);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => worker.InvokeAsync(
                () => worker.SetAcquisitionPriority(AcquisitionPriority.Highest), 1000, default));
            Assert.Equal(2, writes); // Requested write plus verified rollback.
            Assert.Equal("Normal", worker.PriorityStatus.Actual);
            Assert.Null(worker.PriorityStatus.RestoreTarget);
            Assert.NotNull(worker.PriorityStatus.Error);
            Assert.Equal(1, worker.WorkerStarts);
        }
        finally { await worker.CloseAsync(() => { }); }
    }

    [Fact]
    public async Task CachedStatusRemainsReadableWhileSdkIsBlockedAndPollGapIncludesThePause()
    {
        await using var session = await Session.OpenAsync();
        await session.StartAsync();
        await Eventually(async () => (await session.Status()).GetProperty("RxQueue").GetProperty("LastObservationTimestamp").ValueKind == JsonValueKind.Number);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var blocked = session.Worker.InvokeAsync(() => { entered.Set(); release.Wait(); }, 3000, default);
        try
        {
            Assert.True(entered.Wait(2000));
            await Task.Delay(40);
            var status = await session.Status().WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.True(status.GetProperty("RxQueue").GetProperty("ObservationAgeMs").GetDouble() >= 30);
        }
        finally { release.Set(); }
        await blocked;
        await Eventually(async () => (await session.Status()).GetProperty("RxQueue").GetProperty("MaxPollGapMs").GetDouble() >= 30);
    }

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 2500)
        {
            if (await condition()) return;
            await Task.Delay(5);
        }
        Assert.True(await condition(), "condition did not become observable");
    }

    private sealed class Session : IAsyncDisposable
    {
        private readonly string _eni = Path.Combine(Path.GetTempPath(), "zlg-acquisition-" + Guid.NewGuid().ToString("N") + ".xml");
        private ThreadPriority _fakePriority = ThreadPriority.Normal;
        public ThreadPriority? FailPriority;
        public bool ExpectCloseFailure;
        public int Releases;
        public QueueApi Api { get; } = new();
        public SdkWorker Worker { get; private set; } = null!;
        public ZlgChannel Channel { get; }
        private Session(bool injectPriority, Action<RxQueueNotice>? warning)
        {
            File.WriteAllText(_eni, "<EtherCATConfig><Cyclic><CycleTime>250</CycleTime></Cyclic><Slave><DC><CycleTime0>250000</CycleTime0></DC></Slave></EtherCATConfig>");
            Channel = new(Api, "diagnostics", () => Releases++, _ => { }, queueCapacity: 4, warning: warning,
                workerFactory: name => Worker = injectPriority
                    ? new SdkWorker(name, null, value => { if (FailPriority == value) throw new IOException("fake priority rejection"); _fakePriority = value; }, () => _fakePriority)
                    : new SdkWorker(name));
        }
        public static async Task<Session> OpenAsync(bool injectPriority = false, Action<RxQueueNotice>? warning = null)
        {
            var s = new Session(injectPriority, warning);
            await s.Channel.OpenAsync("NO1", 0, 1000, default);
            await s.Run("StartBus", ("EniPath", s._eni), ("ExpectedCycleUs", 250), ("RequireDc", true));
            await s.Run("ConfigureExchange", ("ExchangeMode", ExchangeMode.Queue), ("InitialOutput", Array.Empty<byte>()), ("QueuePrefill", 1));
            await s.Run("EnablePi");
            return s;
        }
        public Task<object?> Run(string op, params (string Key, object Value)[] values) =>
            Channel.ExecuteAsync(op, new("test", 2000, "", values.ToDictionary(v => v.Key, v => (object?)v.Value)), null!, default);
        public Task<object?> StartAsync(AcquisitionPriority priority = AcquisitionPriority.Normal, bool latest = false) => Run("StartAcquisition",
            ("NativeWaitMs", 1), ("HistoryCapacity", 8), ("LatestOnly", latest), ("AcquisitionThreadPriority", priority),
            ("RxHighWatermarkPercent", 80), ("RxBacklogWarningMs", 10), ("RxAlarmRepeatMs", 100));
        public async Task<JsonElement> Status() => JsonSerializer.SerializeToElement(await Run("ReadAcquisitionStatus"));
        public async ValueTask DisposeAsync()
        {
            try { await Channel.CloseAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (InvalidOperationException) when (ExpectCloseFailure) { }
            finally { File.Delete(_eni); }
        }
    }

    private sealed class QueueApi : ZlgNativeApi
    {
        private readonly Queue<byte> _frames = new();
        private byte _input;
        private bool _running, _enabled;
        public int ReadCalls, CloseCalls;
        public ConcurrentDictionary<int, byte> ThreadIds { get; } = new();
        private void Track() => ThreadIds.TryAdd(Environment.CurrentManagedThreadId, 0);
        public void Enqueue(byte value) { lock (_frames) { while (_frames.Count >= 4) _frames.Dequeue(); _frames.Enqueue(value); } }
        public override IntPtr Open(string selector, uint channel) { Track(); return new(1); }
        public override void SetTimeout(IntPtr handle, uint timeout) => Track();
        public override BoardDetails GetBoardInfo(IntPtr handle) { Track(); return new("HPCIe-2E", "1", "test", 0, 1, 0); }
        public override void StartBus(IntPtr handle, string path) { Track(); _running = true; }
        public override void StopBus(IntPtr handle) { Track(); _running = false; }
        public override EniDetails GetEniInfo(IntPtr handle) => new(250, 1000, 0, 1);
        public override ushort GetSlaveCount(IntPtr handle, bool activeOnly = false) => 1;
        public override PiRange GetSlavePiRange(IntPtr handle, PiArea area, uint slave) => new(0, area is PiArea.LocalInput or PiArea.ShadowInput ? 1u : 0u);
        public override void SetPiAccessMode(IntPtr handle, PiAccessMode mode) => Track();
        public override void EnablePi(IntPtr handle, bool enabled) { Track(); _enabled = enabled; }
        public override bool IsPiEnabled(IntPtr handle) => _enabled;
        public override bool IsBusRunning(IntPtr handle) => _running;
        public override byte GetMasterState(IntPtr handle) => 8;
        public override MasterDiagnostics ReadDiagnostics(IntPtr handle, ushort[] slaves) => new(0xA01, 8, 1, 0, []);
        public override uint GetInputQueueSize(IntPtr handle) { Track(); lock (_frames) return (uint)_frames.Count; }
        public override bool PopInput(IntPtr handle, bool latest, uint timeout)
        {
            Track(); lock (_frames)
            {
                if (_frames.Count == 0) return false;
                _input = _frames.Dequeue();
                if (latest) while (_frames.Count > 0) _input = _frames.Dequeue();
                return true;
            }
        }
        public override byte[] ReadPi(IntPtr handle, PiArea area, uint offset, int length) { Track(); Interlocked.Increment(ref ReadCalls); return [_input]; }
        public override void Close(IntPtr handle) { Track(); Interlocked.Increment(ref CloseCalls); }
    }
}
