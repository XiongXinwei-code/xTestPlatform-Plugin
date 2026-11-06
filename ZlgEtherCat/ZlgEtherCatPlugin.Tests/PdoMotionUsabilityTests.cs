using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Xunit;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;
using ZlgEtherCat.Native;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCatPlugin.Tests;

public sealed class PdoMotionUsabilityTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task QueueOutputRejectsMissingModeOrStopFeedbackBeforeStarting(bool mapMode, bool mapVelocity)
    {
        await using var scope = await Session.OpenAsync();
        await scope.ConfigureAndAcquireAsync();
        await scope.BindAsync(mapMode, mapVelocity);
        var before = scope.Api.PushCalls;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("StartCyclicOutput", new() { ["IntervalMs"] = 1, ["CommandLeaseMs"] = 1000 }));
        Assert.Equal(before, scope.Api.PushCalls);
        await scope.RunAsync("UnbindAxis", new() { ["AxisName"] = "A" });
    }

    [Fact]
    public async Task CompleteCspMappingCanStopAndDisableWhileQueueOutputRuns()
    {
        await using var scope = await Session.OpenAsync();
        await scope.ConfigureAndAcquireAsync();
        await scope.BindAsync(true, true);
        await scope.RunAsync("StartCyclicOutput", new() { ["IntervalMs"] = 1, ["CommandLeaseMs"] = 1000 });
        await scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "A", ["Position"] = 10 });
        scope.Api.FollowControl = true;
        await scope.RunAsync("BeginExit");
        await scope.RunAsync("StopMotion", new() { ["AxisName"] = "A", ["QuickStop"] = true, ["VelocityTolerance"] = 0 });
        await scope.RunAsync("DisableAxis", new() { ["AxisName"] = "A" });
        await scope.RunAsync("UnbindAxis", new() { ["AxisName"] = "A" });
        await scope.RunAsync("StopCyclicOutput");
        Assert.Equal(0, scope.Api.SdoCalls);
        Assert.Empty(scope.Errors);
    }

    [Fact]
    public async Task AxisBoundDuringQueueOutputMustHaveStopFeedback()
    {
        await using var scope = await Session.OpenAsync();
        await scope.ConfigureAndAcquireAsync();
        await scope.RunAsync("StartCyclicOutput", new() { ["IntervalMs"] = 1, ["CommandLeaseMs"] = 1000 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.BindAsync(true, false));
        await scope.RunAsync("StopCyclicOutput");
        await scope.RunAsync("Close");
    }

    [Fact]
    public async Task InputOnlyEniDoesNotWriteOrPrefillEmptyOutput()
    {
        await using var scope = await Session.OpenAsync(outputBytes: 0);
        await scope.ConfigureAndAcquireAsync();
        var value = await scope.ReadAsync(false);
        Assert.IsType<ushort>(value);
        Assert.Equal(0, scope.Api.WritePiCalls);
        Assert.Equal(0, scope.Api.PushCalls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("StartCyclicOutput", new() { ["IntervalMs"] = 1, ["CommandLeaseMs"] = 1000 }));
    }

    [Fact]
    public async Task OutputOnlyEniRejectsInputAcquisitionWithoutIsolatingUsableOutput()
    {
        await using var scope = await Session.OpenAsync(inputBytes: 0);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.StartAcquisitionAsync());
        await scope.RunAsync("WritePdo", new() { ["SlaveIndex"] = 0, ["Offset"] = 0, ["Data"] = new byte[] { 0x55 } });
        await scope.RunAsync("ReadMasterStatus");
        Assert.Equal(0, scope.Api.ReadPiCalls);
        Assert.Empty(scope.Errors);
    }

    [Fact]
    public async Task RangeOutsideCompleteEniImageIsRejectedBeforePublishingConfiguration()
    {
        await using var scope = await Session.OpenAsync(startBus: false);
        scope.Api.BadRange = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => scope.StartBusAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ConfigureAsync());
    }

    [Fact]
    public async Task ReadPdoSampleIsOptInAndKeepsExactSequenceForNextWait()
    {
        await using var scope = await Session.OpenAsync();
        await scope.ConfigureAndAcquireAsync();
        Assert.IsType<ushort>(await scope.ReadAsync(false));
        var sample = Assert.IsType<PdoReadSample>(await scope.ReadAsync(true));
        Assert.IsType<ushort>(sample.Value);
        Assert.Equal(0, sample.SlaveIndex);
        Assert.Equal(0, sample.Offset);
        Assert.Equal(2, sample.Length);
        Assert.Equal(PdoDataType.UInt16, sample.DataType);
        var next = Assert.IsType<PdoReadSample>(await scope.RunAsync("WaitNextPdo", new()
        {
            ["SlaveIndex"] = 0, ["Offset"] = 0, ["Length"] = 2, ["DataType"] = PdoDataType.UInt16,
            ["MaxAgeMs"] = 1000, ["AfterSequence"] = sample.Sequence, ["ReturnSample"] = true
        }));
        Assert.True(next.Sequence > sample.Sequence);
        Assert.True(next.ReceivedTimestamp >= sample.ReceivedTimestamp);
    }

    [Fact]
    public async Task MetadataAvailabilityIsReportedPerSlaveAndDirection()
    {
        await using var scope = await Session.OpenAsync(startBus: false);
        scope.AddInputMetadata();
        await scope.StartBusAsync();
        var first = await scope.RunAsync("ReadPiRange", new() { ["SlaveIndex"] = 0 });
        var second = await scope.RunAsync("ReadPiRange", new() { ["SlaveIndex"] = 1 });
        Assert.Equal(true, first!.GetType().GetProperty("HasMetadata")!.GetValue(first));
        Assert.Equal(true, first.GetType().GetProperty("InputMetadataAvailable")!.GetValue(first));
        Assert.Equal(false, first.GetType().GetProperty("OutputMetadataAvailable")!.GetValue(first));
        Assert.Equal(false, second!.GetType().GetProperty("HasMetadata")!.GetValue(second));
        Assert.Equal(true, second.GetType().GetProperty("LayoutHasMetadata")!.GetValue(second));
        Assert.Equal(false, first.GetType().GetProperty("ObjectIdentityVerified")!.GetValue(first));
    }

    [Fact]
    public async Task ActiveBoardPwmRequiresExplicitStopBeforeNormalClose()
    {
        await using var scope = await Session.OpenAsync();
        await scope.RunAsync("StartBoardPwm", new() { ["Port"] = 0, ["FrequencyHz"] = 1000, ["Duty"] = 50 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("Close"));
        Assert.Equal(0, scope.Api.CloseCalls);
        await scope.RunAsync("StopBoardPwm", new() { ["Port"] = 0 });
        await scope.RunAsync("Close");
        Assert.Equal(1, scope.Api.StopPwmCalls);
    }

    [Fact]
    public async Task DisposalExplicitlyStopsAttemptedBoardOutputBeforeClosingHandle()
    {
        await using var scope = await Session.OpenAsync();
        scope.Api.FailPwmStart = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("StartBoardPwm", new() { ["Port"] = 0, ["FrequencyHz"] = 1000, ["Duty"] = 50 }));
        await scope.Channel.CloseAsync();
        var calls = scope.Api.Calls.ToArray();
        Assert.Equal(1, scope.Api.StopPwmCalls);
        Assert.True(Array.IndexOf(calls, "StopPwm") < Array.IndexOf(calls, "Close"));
    }

    [Fact]
    public async Task BoardLeaseIsHeldUntilAllPortsAreExplicitlyStopped()
    {
        await using var scope = await Session.OpenAsync();
        await scope.RunAsync("StartBoardPwm", new() { ["Port"] = 0, ["FrequencyHz"] = 1000, ["Duty"] = 50 });
        await scope.RunAsync("StartBoardPwm", new() { ["Port"] = 1, ["FrequencyHz"] = 1000, ["Duty"] = 50 });
        Assert.Equal(1, scope.BoardLeaseAcquisitions);
        await scope.RunAsync("StopBoardPwm", new() { ["Port"] = 0 });
        Assert.Equal(0, scope.BoardLeaseReleases);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("Close"));
        await scope.RunAsync("StopBoardPwm", new() { ["Port"] = 1 });
        Assert.Equal(1, scope.BoardLeaseReleases);
    }

    [Fact]
    public async Task FailedBoardStopDoesNotSkipOtherPortsOrReleaseUnknownOwnership()
    {
        await using var scope = await Session.OpenAsync();
        await scope.RunAsync("StartBoardPwm", new() { ["Port"] = 0, ["FrequencyHz"] = 1000, ["Duty"] = 50 });
        await scope.RunAsync("StartBoardPwm", new() { ["Port"] = 1, ["FrequencyHz"] = 1000, ["Duty"] = 50 });
        scope.Api.FailStopPort = 0;
        scope.ExpectCloseFailure = true;
        await Assert.ThrowsAsync<AggregateException>(() => scope.Channel.CloseAsync());
        Assert.Equal(2, scope.Api.StopPwmCalls);
        Assert.Equal(1, scope.Api.CloseCalls);
        Assert.Equal(0, scope.Releases);
        Assert.Equal(0, scope.BoardLeaseReleases);
    }

    [Fact]
    public async Task LatchConfigurationCannotBeChangedOrStoppedByAnotherChannel()
    {
        var owner = new SharedBoardOwner();
        await using var first = await Session.OpenAsync(boardOwner: owner.Acquire);
        await using var second = await Session.OpenAsync(boardOwner: owner.Acquire);
        Dictionary<string, object?> Configuration() => new() { ["PortSelection"] = 1, ["LatchMode"] = 0, ["Continuous"] = false };
        await first.RunAsync("ConfigureBoardLatch", Configuration());
        await first.RunAsync("StartBoardLatch", new() { ["Continuous"] = false, ["Count"] = 4 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.RunAsync("ConfigureBoardLatch", Configuration()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.RunAsync("StopBoardLatch"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.RunAsync("ReadBoardLatch", new() { ["Port"] = 0, ["SampleIndex"] = 0, ["Continuous"] = false }));
        Assert.Equal(0, second.Api.LatchConfigureCalls);
        Assert.Equal(42UL, await first.RunAsync("ReadBoardLatch", new() { ["Port"] = 0, ["SampleIndex"] = 0, ["Continuous"] = false }));
        await first.RunAsync("StopBoardLatch");
        await first.RunAsync("Close");
        Assert.Equal(1, first.BoardLeaseReleases);
        await second.RunAsync("ConfigureBoardLatch", Configuration());
    }

    [Fact]
    public async Task FailedLatchStopStillDisablesCaptureAndRetainsUnknownOwnership()
    {
        await using var scope = await Session.OpenAsync();
        await scope.RunAsync("ConfigureBoardLatch", new() { ["PortSelection"] = 1, ["LatchMode"] = 0, ["Continuous"] = false });
        await scope.RunAsync("StartBoardLatch", new() { ["Continuous"] = false, ["Count"] = 1 });
        scope.Api.FailLatchStop = true;
        scope.ExpectCloseFailure = true;
        await Assert.ThrowsAsync<AggregateException>(() => scope.Channel.CloseAsync());
        Assert.Equal(1, scope.Api.StopLatchCalls);
        Assert.Equal(1, scope.Api.DisableLatchCalls);
        Assert.Equal(0, scope.Releases);
        Assert.Equal(0, scope.BoardLeaseReleases);
    }

    [Fact]
    public async Task BeginExitWithdrawsQueuedOperationalRequestBeforeSdkSubmission()
    {
        await using var scope = await Session.OpenAsync();
        scope.Api.BlockSdo = true;
        var blocking = scope.Channel.ReadObjectAsync(0, 0x6041, 0, 2, 1000, default);
        Assert.True(scope.Api.NativeEntered.Wait(2000));
        var request = scope.RunAsync("RequestState", new() { ["State"] = MasterState.Operational, ["PollIntervalMs"] = 1 });
        await scope.RunAsync("BeginExit");
        scope.Api.NativeRelease.Set();
        await blocking;
        await Assert.ThrowsAsync<InvalidOperationException>(() => request);
        Assert.Empty(scope.Api.StateRequests);
        await scope.RunAsync("RequestState", new() { ["State"] = MasterState.SafeOperational, ["PollIntervalMs"] = 1 });
        Assert.Equal(new byte[] { 4 }, scope.Api.StateRequests.ToArray());
    }

    private sealed class SharedBoardOwner
    {
        private int _owned;
        public IDisposable Acquire()
        {
            if (Interlocked.CompareExchange(ref _owned, 1, 0) != 0) throw new InvalidOperationException("板卡被另一通道持有");
            return new Release(() => Interlocked.Exchange(ref _owned, 0));
        }
        private sealed class Release(Action callback) : IDisposable { public void Dispose() => callback(); }
    }

    private sealed class Session : IAsyncDisposable
    {
        private readonly string _eniPath = Path.Combine(Path.GetTempPath(), "ZlgUsability-" + Guid.NewGuid().ToString("N") + ".xml");
        public TestApi Api { get; }
        public ZlgChannel Channel { get; }
        public ConcurrentQueue<Exception> Errors { get; } = new();
        public int Releases, BoardLeaseAcquisitions, BoardLeaseReleases;
        public bool ExpectCloseFailure;
        private Session(uint inputBytes, uint outputBytes, Func<IDisposable>? boardOwner)
        {
            Api = new TestApi { InputBytes = inputBytes, OutputBytes = outputBytes };
            File.WriteAllText(_eniPath, "<EtherCATConfig><Cyclic><CycleTime>250</CycleTime></Cyclic><Slave><DC><CycleTime0>250000</CycleTime0></DC></Slave></EtherCATConfig>", new UTF8Encoding(false));
            Channel = new(Api, "usability", () => Interlocked.Increment(ref Releases), Errors.Enqueue,
                () =>
                {
                    var ownership = boardOwner?.Invoke();
                    Interlocked.Increment(ref BoardLeaseAcquisitions);
                    return new CallbackLease(() => { ownership?.Dispose(); Interlocked.Increment(ref BoardLeaseReleases); });
                }, queueCapacity: 128);
        }
        public static async Task<Session> OpenAsync(uint inputBytes = 32, uint outputBytes = 32, bool startBus = true, Func<IDisposable>? boardOwner = null)
        {
            var session = new Session(inputBytes, outputBytes, boardOwner);
            await session.Channel.OpenAsync("ID0", 0, 1000, default);
            if (startBus) await session.StartBusAsync();
            return session;
        }
        public Task<object?> RunAsync(string op, Dictionary<string, object?>? args = null) => Channel.ExecuteAsync(op, new ZlgRequest("ECAT1", 1000, "", args ?? []), null!, default);
        public Task<object?> StartBusAsync() => RunAsync("StartBus", new() { ["EniPath"] = _eniPath, ["ExpectedCycleUs"] = 250, ["RequireDc"] = true });
        public void AddInputMetadata() => File.WriteAllText(_eniPath,
            "<EtherCATConfig><Cyclic><CycleTime>250</CycleTime></Cyclic><Slave><DC><CycleTime0>250000</CycleTime0></DC></Slave><ProcessImage><Inputs><Variable><Name>FirstStatus</Name><DataType>UINT</DataType><BitSize>16</BitSize><BitOffs>0</BitOffs></Variable></Inputs></ProcessImage></EtherCATConfig>", new UTF8Encoding(false));
        public Task<object?> ConfigureAsync() => RunAsync("ConfigureExchange", new() { ["ExchangeMode"] = ExchangeMode.Queue, ["InitialOutput"] = new byte[Api.OutputBytes], ["QueuePrefill"] = 2 });
        public Task<object?> StartAcquisitionAsync() => RunAsync("StartAcquisition", new() { ["NativeWaitMs"] = 1, ["HistoryCapacity"] = 16, ["LatestOnly"] = false });
        public async Task ConfigureAndAcquireAsync()
        {
            await ConfigureAsync(); await RunAsync("EnablePi"); await StartAcquisitionAsync();
            await RunAsync("WaitNextPdo", new() { ["SlaveIndex"] = 0, ["Offset"] = 0, ["Length"] = 2, ["DataType"] = PdoDataType.UInt16, ["MaxAgeMs"] = 1000, ["AfterSequence"] = 0L });
        }
        public Task<object?> ReadAsync(bool sample) => RunAsync("ReadPdo", new() { ["SlaveIndex"] = 0, ["Offset"] = 0, ["Length"] = 2, ["DataType"] = PdoDataType.UInt16, ["MaxAgeMs"] = 1000, ["ReturnSample"] = sample });
        public Task<object?> BindAsync(bool mode, bool velocity) => RunAsync("BindAxis", new()
        {
            ["AxisName"] = "A", ["SlaveIndex"] = 0, ["AllowedModes"] = "8", ["MaxFeedbackAgeMs"] = 1000,
            ["InputByteOffset"] = 0, ["InputByteLength"] = 16, ["OutputByteOffset"] = 0, ["OutputByteLength"] = 16,
            ["StatusWordOffset"] = 0, ["ActualPositionOffset"] = 2, ["ModeDisplayOffset"] = mode ? 6 : -1,
            ["ActualVelocityOffset"] = velocity ? 8 : -1, ["ControlWordOffset"] = 0, ["TargetPositionOffset"] = 2
        });
        public async ValueTask DisposeAsync()
        {
            Api.NativeRelease.Set();
            try { await Channel.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) when (ExpectCloseFailure) { }
            finally { File.Delete(_eniPath); Api.NativeEntered.Dispose(); Api.NativeRelease.Dispose(); }
        }
        private sealed class CallbackLease(Action release) : IDisposable
        {
            private int _released;
            public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) release(); }
        }
    }

    private sealed class TestApi : ZlgNativeApi
    {
        public uint InputBytes = 32, OutputBytes = 32;
        public bool PiEnabled, Running, BadRange, FollowControl, FailPwmStart;
        public bool FailLatchStop, BlockSdo;
        public int WritePiCalls, ReadPiCalls, PushCalls, SdoCalls, CloseCalls, StopPwmCalls;
        public int LatchConfigureCalls, StopLatchCalls, DisableLatchCalls;
        public uint? FailStopPort;
        public byte MasterStateValue = 8;
        public ConcurrentQueue<byte> StateRequests { get; } = new();
        public ManualResetEventSlim NativeEntered { get; } = new();
        public ManualResetEventSlim NativeRelease { get; } = new();
        public ConcurrentQueue<string> Calls { get; } = new();
        private readonly byte[] _input = new byte[32];
        private byte[] _output = new byte[32];
        private long _nextInput;
        public TestApi() { _input[0] = _input[16] = 0x27; _input[6] = _input[22] = 8; }
        public override IntPtr Open(string selector, uint channel) => new(1);
        public override void SetTimeout(IntPtr handle, uint timeoutMs) { }
        public override void SetBoardTimeout(IntPtr handle, uint timeoutMs) { }
        public override BoardDetails GetBoardInfo(IntPtr handle) => new("PCIe-1E16", "1", "test", 0, 1, 0);
        public override IntPtr GetBoardHandle(IntPtr handle) => new(2);
        public override bool IsPiEnabled(IntPtr handle) => PiEnabled;
        public override bool IsBusRunning(IntPtr handle) => Running;
        public override void StartBus(IntPtr handle, string path) => Running = true;
        public override void StopBus(IntPtr handle) => Running = false;
        public override EniDetails GetEniInfo(IntPtr handle) => new(250, 1000, OutputBytes, InputBytes);
        public override ushort GetSlaveCount(IntPtr handle, bool activeOnly = false) => 2;
        public override PiRange GetSlavePiRange(IntPtr handle, PiArea area, uint slave)
        {
            var length = (area is PiArea.LocalInput or PiArea.ShadowInput ? InputBytes : OutputBytes) / 2;
            return new(BadRange ? 999u : slave * length, length);
        }
        public override byte GetMasterState(IntPtr handle) => MasterStateValue;
        public override void RequestMasterState(IntPtr handle, byte state) { StateRequests.Enqueue(state); MasterStateValue = state; }
        public override uint GetBusStatus(IntPtr handle) => Running ? 1u : 0u;
        public override MasterDiagnostics ReadDiagnostics(IntPtr handle, ushort[] slaves) => new(0xA01, 8, 2, 0, []);
        public override void SetPiAccessMode(IntPtr handle, PiAccessMode mode) { }
        public override void EnablePi(IntPtr handle, bool enabled) => PiEnabled = enabled;
        public override void WritePi(IntPtr handle, PiArea area, uint offset, byte[] data)
        {
            if (data.Length == 0) throw new ArgumentException("禁止零长度原生写入");
            WritePiCalls++; _output = data.ToArray();
        }
        public override void PushOutput(IntPtr handle, bool clearOldQueue, uint timeoutMs)
        {
            if (OutputBytes == 0) throw new InvalidOperationException("无输出映像时不得预填输出队列");
            PushCalls++;
            if (FollowControl) _input[0] = _output[0] switch { 0 => 0x40, 0x0B => 0x07, _ => 0x27 };
        }
        public override uint GetOutputQueueSize(IntPtr handle) => 0;
        public override uint GetInputQueueSize(IntPtr handle) => InputBytes > 0 && Stopwatch.GetTimestamp() >= _nextInput ? 1u : 0;
        public override bool PopInput(IntPtr handle, bool latest, uint timeoutMs) { _nextInput = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500; return true; }
        public override byte[] ReadPi(IntPtr handle, PiArea area, uint offset, int length)
        {
            if (length == 0) throw new ArgumentException("禁止零长度原生读取");
            ReadPiCalls++; return _input.AsSpan(checked((int)offset), length).ToArray();
        }
        public override byte[] ReadSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, int capacity, uint timeoutMs, bool completeAccess = false)
        {
            SdoCalls++;
            if (BlockSdo) { NativeEntered.Set(); NativeRelease.Wait(); }
            return index switch { 0x6041 => [_input[slave * 16], 0], 0x6061 => [8], 0x606C => [0, 0, 0, 0], _ => throw new InvalidOperationException("假设备未定义该对象") };
        }
        public override void OutputPwm(IntPtr boardHandle, uint channel, uint frequency, uint dutyPercent)
        { Calls.Enqueue("StartPwm"); if (FailPwmStart) throw new InvalidOperationException("输出提交结果未确认"); }
        public override void StopPwm(IntPtr boardHandle, uint channel)
        { StopPwmCalls++; Calls.Enqueue("StopPwm"); if (FailStopPort == channel) throw new InvalidOperationException("停止未确认"); }
        public override void ConfigureLatch(IntPtr boardHandle, uint portMap, uint mode, bool continuous) => LatchConfigureCalls++;
        public override void EnableLatch(IntPtr boardHandle, bool enabled) { if (!enabled) DisableLatchCalls++; }
        public override void ClearLatch(IntPtr boardHandle, bool resetCounter) { }
        public override void StartLatch(IntPtr boardHandle, bool continuous, uint count) { }
        public override void StopLatch(IntPtr boardHandle) { StopLatchCalls++; if (FailLatchStop) throw new InvalidOperationException("锁存停止未确认"); }
        public override uint ReadLatchState(IntPtr boardHandle, uint channel) => 1;
        public override ulong ReadLatch(IntPtr boardHandle, uint channel, bool continuous, uint sampleIndex) => 42;
        public override void Close(IntPtr handle) { CloseCalls++; Calls.Enqueue("Close"); }
    }
}
