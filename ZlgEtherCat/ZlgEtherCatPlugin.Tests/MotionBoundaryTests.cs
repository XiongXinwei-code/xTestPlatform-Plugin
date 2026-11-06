using System.Diagnostics;
using Xunit;
using ZlgEtherCat.Motion;

namespace ZlgEtherCat.Tests;

public sealed class MotionBoundaryTests
{
    [Fact]
    public async Task UnreturnedIoRetainsAxisGateAfterSoftTimeout()
    {
        var io = new MotionFakeIo();
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A" }, 100, default);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        io.PendingWrite = release.Task;
        io.WriteEntered = entered;
        var enabling = axis.EnableAsync(1000, default);
        try
        {
            // A timeout before entering I/O legitimately releases the gate; establish the actual pending call first.
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(release.Task.IsCompleted);
            await Assert.ThrowsAsync<TimeoutException>(() => enabling);
            var count = io.ReadCount;
            await Assert.ThrowsAsync<TimeoutException>(() => axis.ReadStateAsync(20, default));
            Assert.Equal(count, io.ReadCount);
            Assert.Equal("A", io.Claimed);
        }
        finally
        {
            release.TrySetResult();
            io.PendingWrite = null;
            // Observe the original task even if the entry assertion fails before the expected timeout assertion.
            _ = enabling.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        await axis.ReadStateAsync(1000, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => axis.EnableAsync(100, default));
    }

    [Fact]
    public async Task BindFailureDoesNotReleaseClaimWhileOriginalIoStillRunning()
    {
        var release = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var io = new MotionFakeIo { PendingRead = release.Task };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A" }, 20, default));
        Assert.Contains("保留轴占用", error.Message);
        Assert.Equal("A", io.Claimed);
        release.SetResult([0x40, 0]);
    }

    [Fact]
    public async Task ConfigurationCopiedAtBind()
    {
        var modes = new[] { MotionMode.PP };
        var io = new MotionFakeIo();
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AllowedModes = modes }, 100, default);
        modes[0] = MotionMode.PV;
        await Assert.ThrowsAsync<InvalidOperationException>(() => axis.SetModeAsync(MotionMode.PV, 100, default));
        Assert.Empty(io.Writes);
    }

    [Fact]
    public async Task PdoQualityFailureDoesNotReturnCachedFeedbackOrSubmitTarget()
    {
        var io = PdoIo();
        var axis = await ZlgMotionAxis.BindAsync(io, PdoConfiguration(), 100, default);
        io.Snapshot = new MotionSnapshot([0x27, 0, 0, 0, 0, 0], 2, Stopwatch.GetTimestamp(), false);
        await Assert.ThrowsAsync<InvalidDataException>(() => axis.ReadFeedbackAsync(100, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => axis.WriteCyclicTargetAsync(MotionMode.CSP, 123, 100, default));
        Assert.Empty(io.PdoWrites);
    }

    [Fact]
    public async Task PdoTargetAndControlWordSubmittedTogether()
    {
        var io = PdoIo();
        var axis = await ZlgMotionAxis.BindAsync(io, PdoConfiguration(), 100, default);
        await axis.WriteCyclicTargetAsync(MotionMode.CSP, 123, 100, default);
        var writes = Assert.Single(io.PdoWrites);
        Assert.Equal(2, writes.Count);
        Assert.Contains(writes, x => x.ByteOffset == 2 && BitConverter.ToInt32(x.Data) == 123);
        Assert.Contains(writes, x => x.ByteOffset == 0 && BitConverter.ToUInt16(x.Data) == 15);
        Assert.Empty(io.Writes);
    }

    [Fact]
    public async Task CyclicStopDoesNotAssumeHaltSupport()
    {
        var io = PdoIo();
        var axis = await ZlgMotionAxis.BindAsync(io, PdoConfiguration(), 100, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => axis.StopMotionAsync(false, 0, 100, default));
        Assert.Empty(io.PdoWrites);
    }

    [Fact]
    public async Task UnbindNeverClosesConnectionAndRejectsStillEnabledControlledAxis()
    {
        var io = new MotionFakeIo();
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A" }, 500, default);
        await axis.EnableAsync(500, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => axis.UnbindAsync(100, default));
        Assert.Equal("A", io.Claimed);
        await axis.DisableAsync(100, default);
        await axis.UnbindAsync(100, default);
        Assert.Null(io.Claimed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => axis.ReadStateAsync(100, default));
    }

    [Fact]
    public async Task SdoFeedbackIsExplicitlyNotAtomicSnapshot()
    {
        var io = new MotionFakeIo();
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration(), 100, default);
        var feedback = await axis.ReadFeedbackAsync(100, default);
        Assert.Equal(0, feedback.Sequence);
    }

    [Fact]
    public async Task FaultStateCannotEnableOrImplicitlyReset()
    {
        var io = new MotionFakeIo { StatusWord = 8 };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration(), 100, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => axis.EnableAsync(100, default));
        Assert.Empty(io.Writes);
    }

    [Fact]
    public async Task FaultedVelocityAxisDoesNotWriteInitialTargetBeforeRejectingEnable()
    {
        var io = new MotionFakeIo { StatusWord = 8, Mode = 3 };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AllowedModes = [MotionMode.PV] }, 100, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => axis.EnableAsync(100, default));
        Assert.Empty(io.Writes);
    }

    [Fact]
    public async Task ExplicitStopInterruptsPendingPositionWaitBeforeItsOriginalTimeout()
    {
        var io = new MotionFakeIo { StatusWord = 0x0027 };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { PollIntervalMs = 1 }, 200, default);
        var moving = axis.MovePositionAsync(100, false, 10, 10, 10, 2000, default);
        await Task.Delay(10);
        await axis.StopMotionAsync(false, 0, 300, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => moving);
        Assert.Contains(io.Writes, w => w.Index == 0x6040 && (BitConverter.ToUInt16(w.Data) & 0x100) != 0);
    }

    [Fact]
    public async Task HomingErrorIsReportedInsteadOfReachedSuccess()
    {
        var io = new MotionFakeIo { Mode = 6, StatusWord = 0x0027 };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AllowedModes = [MotionMode.HM] }, 100, default);
        io.StatusReads.Enqueue(0x0027);
        io.StatusReads.Enqueue(0x3427);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => axis.HomeAsync(1, 100, 10, 100, 100, default));
        Assert.Contains("回零失败", error.Message);
    }

    [Fact]
    public void SnapshotCopiesInputAndDoesNotExposeMutableBackingArray()
    {
        byte[] original = [1, 2];
        var snapshot = new MotionSnapshot(original, 1, Stopwatch.GetTimestamp(), true);
        original[0] = 9;
        Assert.Equal((byte)1, snapshot.InputImage.Span[0]);
    }

    private static MotionFakeIo PdoIo() => new()
    {
        Mode = 8,
        Snapshot = new MotionSnapshot([0x27, 0, 0, 0, 0, 0], 1, Stopwatch.GetTimestamp(), true)
    };

    private static MotionAxisConfiguration PdoConfiguration() => new()
    {
        AxisName = "A", AllowedModes = [MotionMode.CSP], InputByteLength = 6, OutputByteLength = 6,
        StatusWordOffset = 0, ActualPositionOffset = 2, ControlWordOffset = 0, TargetPositionOffset = 2,
        MaxFeedbackAgeMs = 1000
    };
}
