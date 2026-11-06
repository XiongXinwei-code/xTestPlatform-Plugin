using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Xunit;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;
using ZlgEtherCat.Motion;
using ZlgEtherCat.Native;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCatPlugin.Tests;

public sealed class ChannelRuntimeTests
{
    [Theory]
    [InlineData(250)]
    [InlineData(500)]
    public async Task EnclosedLifecycleUsesConfirmedEniAndDcThenReleasesExactlyOnce(int cycle)
    {
        await using var scope = await ChannelScope.OpenAsync(cycle);
        await scope.StartBusAsync(cycle);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await scope.RunAsync("StartAcquisition", new() { ["NativeWaitMs"] = 1, ["HistoryCapacity"] = 8, ["LatestOnly"] = false });
        await scope.RunAsync("StopAcquisition");
        await scope.RunAsync("DisablePi");
        await scope.RunAsync("StopBus");
        await scope.RunAsync("Close");
        Assert.Equal(1, scope.Releases);
        Assert.Equal(1, scope.Api.CloseCalls);
        Assert.Equal(cycle, (int)scope.Api.Cycle);
        Assert.Empty(scope.Errors);
        Assert.Single(scope.Api.ThreadIds);
    }

    [Fact]
    public async Task EniCycleMismatchRejectedBeforeStartingBus()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await Assert.ThrowsAsync<ArgumentException>(() => scope.StartBusAsync(500));
        Assert.Equal(0, scope.Api.StartCalls);
    }

    [Fact]
    public async Task SdkCycleMismatchIsolatesButCloseRemainsAvailable()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        scope.Api.Cycle = 500;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.StartBusAsync(250));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("ReadMasterStatus"));
        await scope.RunAsync("Close");
        Assert.Equal(1, scope.Releases);
    }

    [Fact]
    public async Task DcNotReadyCannotEnablePi()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        scope.Api.DiagnosticState = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("EnablePi"));
        Assert.False(scope.Api.PiEnabled);
    }

    [Fact]
    public async Task BoundAxisPreventsRawPdoOverwriteAndExplicitClose()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        scope.Channel.ClaimAxis(Axis("A", 0));
        var writes = scope.Api.OutputWrites.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("WritePdo", new() { ["SlaveIndex"] = 0, ["Offset"] = 0, ["Data"] = new byte[] { 1 } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("Close"));
        Assert.Equal(writes, scope.Api.OutputWrites.Count);
        scope.Channel.ReleaseAxis("A");
        await scope.RunAsync("Close");
    }

    [Fact]
    public async Task TimedOutNativeCallKeepsReservationUntilActualClose()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        scope.Api.BlockBusStatus = true;
        var read = scope.RunAsync("ReadMasterStatus", timeoutMs: 50);
        Assert.True(scope.Api.NativeEntered.Wait(2000));
        await Assert.ThrowsAsync<TimeoutException>(() => read);
        await Assert.ThrowsAsync<TimeoutException>(() => scope.RunAsync("Close", timeoutMs: 50));
        Assert.Equal(0, scope.Api.CloseCalls);
        Assert.Equal(0, scope.Releases);
        scope.Api.NativeRelease.Set();
        await scope.Channel.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, scope.Api.CloseCalls);
        Assert.Equal(1, scope.Releases);
    }

    [Theory]
    [InlineData(250, 40)]
    [InlineData(500, 20)]
    public async Task QueueReplenishmentCoversTenMillisecondLookAheadOnOneSdkWorker(int cycle, int frames)
    {
        await using var scope = await ChannelScope.OpenAsync(cycle);
        await scope.StartBusAsync(cycle);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await scope.RunAsync("StartCyclicOutput", new() { ["IntervalMs"] = 1, ["CommandLeaseMs"] = 1000 });
        await UntilAsync(() => scope.Api.QueueDepth >= frames || !scope.Errors.IsEmpty);
        Assert.Empty(scope.Errors);
        Assert.Equal(frames, scope.Api.QueueDepth);
        Assert.Single(scope.Api.ThreadIds);
        await scope.RunAsync("StopCyclicOutput");
    }

    [Fact]
    public async Task QueueReplenishmentMustNotExceedActualHardwareQueueCapacity()
    {
        await using var scope = await ChannelScope.OpenAsync(250, 8);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await scope.RunAsync("StartCyclicOutput", new() { ["IntervalMs"] = 1, ["CommandLeaseMs"] = 1000 });
        await UntilAsync(() => scope.Api.QueueDepth >= 8 || !scope.Errors.IsEmpty);
        await Task.Delay(10);
        Assert.Empty(scope.Errors);
        Assert.InRange(scope.Api.QueueDepth, 1, 8);
    }

    [Fact]
    public async Task RejectedQueueCapacityKeepsPreviouslyConfiguredDirectModeUsable()
    {
        await using var scope = await ChannelScope.OpenAsync(250, 2);
        await scope.StartBusAsync(250);
        await scope.RunAsync("ConfigureExchange", new()
        {
            ["ExchangeMode"] = ExchangeMode.Direct, ["InitialOutput"] = new byte[32], ["QueuePrefill"] = 2
        });
        var writes = scope.Api.OutputWrites.Count;
        await Assert.ThrowsAsync<ArgumentException>(() => scope.RunAsync("ConfigureExchange", new()
        {
            ["ExchangeMode"] = ExchangeMode.Queue, ["InitialOutput"] = new byte[32], ["QueuePrefill"] = 3
        }));
        var status = await scope.RunAsync("ReadExchangeStatus");
        Assert.Equal(ExchangeMode.Direct, status!.GetType().GetProperty("Mode")!.GetValue(status));
        Assert.Equal(PiAccessMode.Direct, scope.Api.AccessMode);
        Assert.Equal(writes, scope.Api.OutputWrites.Count);
        Assert.Empty(scope.Api.SubmittedFrames);
        await scope.RunAsync("EnablePi");
        Assert.True(scope.Api.PiEnabled);
        Assert.Empty(scope.Errors);
    }

    [Fact]
    public async Task OtherAxisCommandsCannotRenewStaleAxisLease()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await scope.StartFeedbackAsync();
        await scope.BindCspAsync("A", 0);
        await scope.BindCspAsync("B", 1);
        await scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "A", ["Position"] = 1 });
        await scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "B", ["Position"] = 1 });
        await scope.RunAsync("StartCyclicOutput", new() { ["IntervalMs"] = 1, ["CommandLeaseMs"] = 250 });
        await scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "B", ["Position"] = 2 });
        var successfulRenewals = 1;
        var deadline = Stopwatch.StartNew();
        while (scope.Errors.IsEmpty && deadline.ElapsedMilliseconds < 1000)
        {
            await Task.Delay(15);
            try { await scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "B", ["Position"] = 1 }); successfulRenewals++; }
            catch (InvalidOperationException error) when (error.Message.Contains("轴", StringComparison.Ordinal) && error.Message.Contains("过期", StringComparison.Ordinal)) { break; }
        }
        await UntilAsync(() => !scope.Errors.IsEmpty);
        Assert.True(successfulRenewals > 1, "B 轴必须在 A 轴未续期期间成功提交多个目标，才构成逐轴隔离证据。");
        Assert.Contains(scope.Errors, error => error.ToString().Contains("轴", StringComparison.Ordinal));
        scope.Channel.ReleaseAxis("A");
        scope.Channel.ReleaseAxis("B");
    }

    [Fact]
    public async Task MotionReceiptWaitsForNativeSubmissionAndPreservesControlWordEdges()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        scope.Channel.ClaimAxis(Axis("A", 0));
        scope.Api.BlockOutputPush = true;
        var low = scope.Channel.SubmitPdoAsync("A", [new(0, [0, 0])], 1000, default);
        Assert.True(scope.Api.NativeEntered.Wait(2000));
        Assert.False(low.IsCompleted);
        scope.Api.NativeRelease.Set();
        await low;
        await scope.Channel.SubmitPdoAsync("A", [new(0, [0x80, 0])], 1000, default);
        Assert.Equal(new byte[] { 0, 0x80 }, scope.Api.SubmittedFrames.Skip(2).Select(frame => frame[0]).ToArray());
        Assert.Single(scope.Api.ThreadIds);
        scope.Channel.ReleaseAxis("A");
    }

    [Fact]
    public async Task ExpiredMotionFrameIsNotReplayedWhenQueueLaterBecomesAvailable()
    {
        await using var scope = await ChannelScope.OpenAsync(250, 2);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        scope.Channel.ClaimAxis(Axis("A", 0));
        await Assert.ThrowsAsync<TimeoutException>(() => scope.Channel.SubmitPdoAsync("A", [new(0, [0x80, 0])], 50, default));
        Interlocked.Exchange(ref scope.Api.QueueDepth, 0);
        await Task.Delay(20);
        Assert.Equal(2, scope.Api.SubmittedFrames.Count);
        await scope.RunAsync("Close");
        Assert.Equal(1, scope.Releases);
    }

    [Fact]
    public async Task QueuedRawPdoWaitsForCapacityInsteadOfIsolating()
    {
        await using var scope = await ChannelScope.OpenAsync(250, 2);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await scope.RunAsync("WritePdo", new() { ["SlaveIndex"] = 0, ["Offset"] = 0, ["Data"] = new byte[] { 42 } });
        await Task.Delay(20);
        Assert.Empty(scope.Errors);
        Assert.Equal(2, scope.Api.SubmittedFrames.Count);
        Interlocked.Exchange(ref scope.Api.QueueDepth, 0);
        await UntilAsync(() => scope.Api.SubmittedFrames.Count > 2 || !scope.Errors.IsEmpty);
        Assert.Empty(scope.Errors);
        Assert.Equal(42, scope.Api.SubmittedFrames.Last()[0]);
    }

    [Fact]
    public async Task FailedBindNativeTimeoutCanCloseAfterActualNativeExit()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await scope.StartBusAsync(250);
        scope.Api.BlockSdo = true;
        var bind = scope.RunAsync("BindAxis", new() { ["AxisName"] = "A", ["SlaveIndex"] = 0, ["AllowedModes"] = "8" }, 50);
        Assert.True(scope.Api.NativeEntered.Wait(2000));
        await Assert.ThrowsAnyAsync<Exception>(() => bind);
        await Assert.ThrowsAsync<TimeoutException>(() => scope.RunAsync("Close", timeoutMs: 50));
        Assert.Equal(0, scope.Releases);
        scope.Api.NativeRelease.Set();
        await scope.RunAsync("Close");
        Assert.Equal(1, scope.Releases);
    }

    [Fact]
    public async Task TargetQueuedDuringSdkStallCannotEraseExpiredAxisLease()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await scope.StartFeedbackAsync();
        await scope.BindCspAsync("A", 0);
        await scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "A", ["Position"] = 1 });
        await scope.RunAsync("StartCyclicOutput", new() { ["IntervalMs"] = 1, ["CommandLeaseMs"] = 150 });
        scope.Api.BlockBusStatus = true;
        var blocked = scope.RunAsync("ReadMasterStatus");
        Assert.True(scope.Api.NativeEntered.Wait(2000));
        var target = scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "A", ["Position"] = 2 });
        await Task.Delay(200);
        scope.Api.NativeRelease.Set();
        await blocked;
        await Assert.ThrowsAnyAsync<Exception>(() => target);
        await UntilAsync(() => !scope.Errors.IsEmpty);
        Assert.Contains(scope.Errors, error => error is TimeoutException);
        scope.Channel.ReleaseAxis("A");
    }

    [Fact]
    public async Task BeginExitRejectsNewTargetsButAllowsStopDisableAndUnbind()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await scope.StartFeedbackAsync();
        await scope.BindCspAsync("A", 0);
        await scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "A", ["Position"] = 1 });
        scope.Api.FollowControlWord = true;
        await scope.RunAsync("BeginExit");
        var sent = scope.Api.SubmittedFrames.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "A", ["Position"] = 2 }));
        Assert.Equal(sent, scope.Api.SubmittedFrames.Count);
        await scope.RunAsync("StopMotion", new() { ["AxisName"] = "A", ["QuickStop"] = true, ["VelocityTolerance"] = 0 });
        await scope.RunAsync("DisableAxis", new() { ["AxisName"] = "A" });
        await scope.RunAsync("UnbindAxis", new() { ["AxisName"] = "A" });
        await scope.RunAsync("StopAcquisition");
        await scope.RunAsync("DisablePi");
        await scope.RunAsync("StopBus");
        await scope.RunAsync("Close");
        Assert.Equal(1, scope.Releases);
    }

    [Fact]
    public async Task BeginExitWithdrawsUnsentTargetWithoutBlockingRecovery()
    {
        await using var scope = await ChannelScope.OpenAsync(250, 2);
        await scope.StartBusAsync(250);
        await scope.ConfigureAsync();
        await scope.RunAsync("EnablePi");
        await scope.StartFeedbackAsync();
        await scope.BindCspAsync("A", 0);
        var target = scope.RunAsync("WriteCspTarget", new() { ["AxisName"] = "A", ["Position"] = 2 });
        await scope.RunAsync("ReadMasterStatus");
        Assert.False(target.IsCompleted);
        await scope.RunAsync("BeginExit");
        Interlocked.Exchange(ref scope.Api.QueueDepth, 0);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => target);
        Assert.Equal(2, scope.Api.SubmittedFrames.Count);
        await scope.RunAsync("ReadMasterStatus");
        scope.Api.FollowControlWord = true;
        await scope.RunAsync("StopMotion", new() { ["AxisName"] = "A", ["QuickStop"] = true, ["VelocityTolerance"] = 0 });
        await scope.RunAsync("DisableAxis", new() { ["AxisName"] = "A" });
        await scope.RunAsync("UnbindAxis", new() { ["AxisName"] = "A" });
        Assert.Empty(scope.Errors);
    }

    [Fact]
    public async Task BeginExitRejectsBindWaitingForChannelGate()
    {
        await using var scope = await ChannelScope.OpenAsync(250);
        await scope.StartBusAsync(250);
        scope.Api.BlockBusStatus = true;
        var read = scope.RunAsync("ReadMasterStatus");
        Assert.True(scope.Api.NativeEntered.Wait(2000));
        var bind = scope.RunAsync("BindAxis", new() { ["AxisName"] = "A", ["SlaveIndex"] = 0, ["AllowedModes"] = "8" });
        Assert.False(bind.IsCompleted);
        await scope.RunAsync("BeginExit");
        scope.Api.NativeRelease.Set();
        await read;
        await Assert.ThrowsAsync<InvalidOperationException>(() => bind);
        await scope.RunAsync("Close");
        Assert.Equal(1, scope.Releases);
    }

    private static MotionAxisConfiguration Axis(string name, ushort slave) => new()
    {
        AxisName = name, SlaveIndex = slave, InputByteOffset = slave * 16, InputByteLength = 16,
        OutputByteOffset = slave * 16, OutputByteLength = 16, ControlWordOffset = slave * 16
    };

    private static async Task UntilAsync(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.ElapsedMilliseconds > 2000) throw new TimeoutException("假设备运行时条件未到达。");
            await Task.Delay(1);
        }
    }

    private sealed class ChannelScope : IAsyncDisposable
    {
        private readonly string _eniPath;
        private int _releases;
        public int Releases => Volatile.Read(ref _releases);
        public FakeChannelApi Api { get; }
        public ConcurrentQueue<Exception> Errors { get; } = new();
        public ZlgChannel Channel { get; }
        private ChannelScope(int cycle, uint capacity)
        {
            Api = new FakeChannelApi { Cycle = (uint)cycle, QueueCapacity = capacity };
            _eniPath = Path.Combine(Path.GetTempPath(), "ZlgChannelTest-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(_eniPath, $"<EtherCATConfig><Config><Cyclic><CycleTime>{cycle}</CycleTime></Cyclic><Slave><DC><CycleTime0>{cycle * 1000}</CycleTime0></DC></Slave></Config></EtherCATConfig>", new UTF8Encoding(false));
            Channel = new ZlgChannel(Api, "test", () => Interlocked.Increment(ref _releases), Errors.Enqueue, queueCapacity: capacity);
        }
        public static async Task<ChannelScope> OpenAsync(int cycle, uint capacity = 128)
        {
            var scope = new ChannelScope(cycle, capacity);
            await scope.Channel.OpenAsync("ID0", 0, 1000, default);
            return scope;
        }
        public Task<object?> RunAsync(string operation, Dictionary<string, object?>? args = null, int timeoutMs = 1000)
            => Channel.ExecuteAsync(operation, new("ECAT1", timeoutMs, "", args ?? []), null!, default);
        public Task<object?> StartBusAsync(int expectedCycle) => RunAsync("StartBus", new() { ["EniPath"] = _eniPath, ["ExpectedCycleUs"] = expectedCycle, ["RequireDc"] = true });
        public Task<object?> ConfigureAsync() => RunAsync("ConfigureExchange", new() { ["ExchangeMode"] = ExchangeMode.Queue, ["InitialOutput"] = new byte[32], ["QueuePrefill"] = 2 });
        public async Task StartFeedbackAsync()
        {
            await RunAsync("StartAcquisition", new() { ["NativeWaitMs"] = 1, ["HistoryCapacity"] = 8, ["LatestOnly"] = false });
            var image = new byte[32]; image[0] = image[16] = 0x27; image[6] = image[22] = 8;
            Api.InputFrames.Enqueue(image);
            Api.AutoFeedback = true;
            await RunAsync("WaitNextPdo", new() { ["AfterSequence"] = 0L, ["MaxAgeMs"] = 1000, ["SlaveIndex"] = 0, ["Offset"] = 0, ["Length"] = 2, ["DataType"] = PdoDataType.UInt16 });
        }
        public Task<object?> BindCspAsync(string name, ushort slave) => RunAsync("BindAxis", new()
        {
            ["AxisName"] = name, ["SlaveIndex"] = slave, ["AllowedModes"] = "8", ["MaxFeedbackAgeMs"] = 1000,
            ["InputByteOffset"] = slave * 16, ["InputByteLength"] = 16,
            ["OutputByteOffset"] = slave * 16, ["OutputByteLength"] = 16,
            ["StatusWordOffset"] = slave * 16, ["ActualPositionOffset"] = slave * 16 + 2,
            ["ModeDisplayOffset"] = slave * 16 + 6, ["ActualVelocityOffset"] = slave * 16 + 8,
            ["ControlWordOffset"] = slave * 16, ["TargetPositionOffset"] = slave * 16 + 2
        });
        public async ValueTask DisposeAsync()
        {
            Api.NativeRelease.Set();
            await Channel.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2));
            File.Delete(_eniPath);
            Api.NativeEntered.Dispose();
            Api.NativeRelease.Dispose();
        }
    }

    private sealed class FakeChannelApi : ZlgNativeApi
    {
        public uint Cycle = 250, DiagnosticState = 0xA01, QueueCapacity = 128;
        public bool PiEnabled, BusRunning, BlockBusStatus, BlockOutputPush, BlockSdo, AutoFeedback, FollowControlWord;
        public int StartCalls, CloseCalls, QueueDepth;
        public PiAccessMode AccessMode;
        public readonly ManualResetEventSlim NativeEntered = new(), NativeRelease = new();
        public readonly ConcurrentDictionary<int, byte> ThreadIds = new();
        public readonly ConcurrentQueue<byte[]> OutputWrites = new();
        public readonly ConcurrentQueue<byte[]> SubmittedFrames = new();
        public readonly ConcurrentQueue<byte[]> InputFrames = new();
        private byte[] _inputImage = new byte[32];
        private byte[] _outputImage = new byte[32];
        private long _nextFeedback;
        private void Track() => ThreadIds.TryAdd(Environment.CurrentManagedThreadId, 0);
        public override IntPtr Open(string boardInterface, uint channel) { Track(); return new IntPtr(1); }
        public override void SetTimeout(IntPtr handle, uint timeoutMs) => Track();
        public override BoardDetails GetBoardInfo(IntPtr handle) { Track(); return new("HPCIe-2E-SM", "1", "test", 1, 1, 1); }
        public override DriverDetails GetDriverInfo() { Track(); return new("1", "1", 1); }
        public override BoardDetails EnumerateBoard(uint index) => GetBoardInfo(new IntPtr(1));
        public override uint GetQueueCapacity(uint boardIndex, uint channel) { Track(); return QueueCapacity; }
        public override bool IsPiEnabled(IntPtr handle) { Track(); return PiEnabled; }
        public override bool IsBusRunning(IntPtr handle) { Track(); return BusRunning; }
        public override void StartBus(IntPtr handle, string eniPath) { Track(); BusRunning = true; StartCalls++; }
        public override void StopBus(IntPtr handle) { Track(); BusRunning = false; }
        public override EniDetails GetEniInfo(IntPtr handle) { Track(); return new(Cycle, 1000, 32, 32); }
        public override ushort GetSlaveCount(IntPtr handle, bool activeOnly = false) { Track(); return 2; }
        public override PiRange GetSlavePiRange(IntPtr handle, PiArea area, uint slave) { Track(); return new(slave * 16, 16); }
        public override byte GetMasterState(IntPtr handle) { Track(); return 8; }
        public override MasterDiagnostics ReadDiagnostics(IntPtr handle, ushort[] slaves) { Track(); return new(DiagnosticState, 8, 2, 0, []); }
        public override void SetPiAccessMode(IntPtr handle, PiAccessMode mode) { Track(); AccessMode = mode; }
        public override void WritePi(IntPtr handle, PiArea area, uint offset, byte[] data)
        { Track(); _outputImage = (byte[])data.Clone(); OutputWrites.Enqueue((byte[])data.Clone()); }
        public override void PushOutput(IntPtr handle, bool clearOldQueue, uint timeoutMs)
        {
            Track();
            if (BlockOutputPush) { NativeEntered.Set(); NativeRelease.Wait(); }
            if (QueueDepth >= QueueCapacity) throw new InvalidOperationException("假设备输出队列容量已满。");
            SubmittedFrames.Enqueue((byte[])_outputImage.Clone());
            if (FollowControlWord)
            {
                _inputImage[0] = _outputImage[0] switch { 0 => 0x40, 0x0B => 0x07, _ => 0x27 };
                InputFrames.Enqueue((byte[])_inputImage.Clone());
            }
            Interlocked.Increment(ref QueueDepth);
        }
        public override uint GetOutputQueueSize(IntPtr handle) { Track(); return (uint)Volatile.Read(ref QueueDepth); }
        public override uint GetInputQueueSize(IntPtr handle)
        {
            Track();
            return InputFrames.IsEmpty && AutoFeedback && Stopwatch.GetTimestamp() >= _nextFeedback ? 1u : (uint)InputFrames.Count;
        }
        public override bool PopInput(IntPtr handle, bool latest, uint timeoutMs)
        {
            Track();
            if (InputFrames.TryDequeue(out var image)) _inputImage = image;
            else if (!AutoFeedback || Stopwatch.GetTimestamp() < _nextFeedback) return false;
            _nextFeedback = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500;
            return true;
        }
        public override byte[] ReadPi(IntPtr handle, PiArea area, uint offset, int length)
        { Track(); return _inputImage.AsSpan(checked((int)offset), length).ToArray(); }
        public override byte[] ReadSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, int capacity, uint timeoutMs, bool completeAccess = false)
        {
            Track();
            if (BlockSdo) { NativeEntered.Set(); NativeRelease.Wait(); }
            if (index == 0x6061) return [8];
            if (index == 0x6041) return [0x40, 0];
            if (index == 0x606C) return [0, 0, 0, 0];
            throw new InvalidOperationException($"未定义假设备 SDO {index:X4}。");
        }
        public override void EnablePi(IntPtr handle, bool enabled) { Track(); PiEnabled = enabled; }
        public override void Close(IntPtr handle) { Track(); Interlocked.Increment(ref CloseCalls); }
        public override uint GetBusStatus(IntPtr handle)
        {
            Track();
            if (BlockBusStatus) { NativeEntered.Set(); NativeRelease.Wait(); }
            return BusRunning ? 1u : 0u;
        }
    }
}
