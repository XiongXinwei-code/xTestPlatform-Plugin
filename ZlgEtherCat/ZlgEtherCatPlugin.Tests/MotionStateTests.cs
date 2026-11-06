using Xunit;
using ZlgEtherCat.Motion;

namespace ZlgEtherCat.Tests;

public sealed class MotionStateTests
{
    [Fact]
    public async Task BindingClaimsAxisButNeverEnablesOrWrites()
    {
        var io = new MotionFakeIo { StatusWord = 0x0027 };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A", AllowedModes = [MotionMode.PP] }, 200, default);
        Assert.Equal("A", io.Claimed);
        Assert.Empty(io.Writes);
        Assert.Equal(MotionDriveState.OperationEnabled, await axis.ReadStateAsync(200, default));
    }

    [Fact]
    public async Task EnableRequiresConfirmedStatusTransitions()
    {
        var io = new MotionFakeIo();
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A" }, 200, default);
        await axis.EnableAsync(200, default);
        Assert.Equal(new ushort[] { 6, 7, 15 }, io.Writes.Where(w => w.Index == 0x6040).Select(w => BitConverter.ToUInt16(w.Data)));
        Assert.Equal(MotionDriveState.OperationEnabled, ZlgMotionAxis.DecodeState(io.StatusWord));
    }

    [Fact]
    public async Task EnableCannotSucceedWhenDriveNeverTransitions()
    {
        var io = new MotionFakeIo { FreezeStatus = true };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A", PollIntervalMs = 1 }, 100, default);
        await Assert.ThrowsAsync<TimeoutException>(() => axis.EnableAsync(30, default));
        Assert.NotEqual((ushort)0x0027, io.StatusWord);
    }

    [Fact]
    public async Task PositionMoveWaitsForNewAcknowledgementAndTargetReached()
    {
        var io = new MotionFakeIo { StatusWord = 0x0027, AutoCompletePosition = true };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A", PollIntervalMs = 1 }, 200, default);
        await axis.MovePositionAsync(123, true, 100, 50, 50, 200, default);
        Assert.Contains(io.Writes, w => w.Index == 0x607A && BitConverter.ToInt32(w.Data) == 123);
        Assert.Contains(io.Writes, w => w.Index == 0x6040 && (BitConverter.ToUInt16(w.Data) & 0x50) == 0x50);
        Assert.Equal(0, BitConverter.ToUInt16(io.Writes.Last(w => w.Index == 0x6040).Data) & 0x10);
    }

    [Fact]
    public async Task StaleTargetReachedWithoutAcknowledgementIsNotMoveSuccess()
    {
        var io = new MotionFakeIo { StatusWord = 0x0427 };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A", PollIntervalMs = 1 }, 100, default);
        await Assert.ThrowsAsync<TimeoutException>(() => axis.MovePositionAsync(123, false, 100, 50, 50, 30, default));
    }

    [Fact]
    public async Task ModeSwitchWaitsForDisplayAndRejectsEnabledDrive()
    {
        var io = new MotionFakeIo { FreezeMode = true };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A", AllowedModes = [MotionMode.PP, MotionMode.PV], PollIntervalMs = 1 }, 100, default);
        await Assert.ThrowsAsync<TimeoutException>(() => axis.SetModeAsync(MotionMode.PV, 30, default));
        var enabledIo = new MotionFakeIo { StatusWord = 0x0027 };
        var enabled = await ZlgMotionAxis.BindAsync(enabledIo, new MotionAxisConfiguration { AxisName = "B", AllowedModes = [MotionMode.PP, MotionMode.PV] }, 100, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => enabled.SetModeAsync(MotionMode.PV, 100, default));
        Assert.Empty(enabledIo.Writes);
    }

    [Fact]
    public async Task CancellationRemainsCancellation()
    {
        var io = new MotionFakeIo { FreezeStatus = true };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A", PollIntervalMs = 1 }, 100, default);
        using var cts = new CancellationTokenSource(20);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => axis.EnableAsync(1000, cts.Token));
    }

    [Fact]
    public async Task OverlappingPdoFieldsRejectedBeforeClaim()
    {
        var io = new MotionFakeIo();
        await Assert.ThrowsAsync<ArgumentException>(() => ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration
        {
            AxisName = "A", OutputByteLength = 8, ControlWordOffset = 0, TargetPositionOffset = 1
        }, 100, default));
        Assert.Null(io.Claimed);
    }

    [Fact]
    public async Task CyclicTargetRequiresMappingAndNeverWritesSdoTarget()
    {
        var io = new MotionFakeIo { StatusWord = 0x0027, Mode = 8 };
        var axis = await ZlgMotionAxis.BindAsync(io, new MotionAxisConfiguration { AxisName = "A", AllowedModes = [MotionMode.CSP] }, 100, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => axis.WriteCyclicTargetAsync(MotionMode.CSP, 10, 100, default));
        Assert.Empty(io.Writes);
    }
}

internal sealed class MotionFakeIo : IZlgMotionIo
{
    public ushort StatusWord { get; set; } = 0x0040;
    public sbyte Mode { get; set; } = 1;
    public bool FreezeStatus { get; set; }
    public bool FreezeMode { get; set; }
    public bool AutoCompletePosition { get; set; }
    public Task? PendingWrite { get; set; }
    public TaskCompletionSource? WriteEntered { get; set; }
    public Task<byte[]>? PendingRead { get; set; }
    public int ReadCount { get; private set; }
    public MotionSnapshot Snapshot { get; set; } = new([], 1, System.Diagnostics.Stopwatch.GetTimestamp(), true);
    public List<IReadOnlyList<MotionPdoWrite>> PdoWrites { get; } = [];
    public Queue<ushort> StatusReads { get; } = new();
    public string? Claimed { get; private set; }
    public List<(ushort Index, byte[] Data)> Writes { get; } = [];
    public void ClaimAxis(MotionAxisConfiguration configuration) => Claimed = configuration.AxisName;
    public void ReleaseAxis(string axisName) => Claimed = null;
    public Task<byte[]> ReadObjectAsync(ushort slave, ushort index, byte subIndex, int expectedLength, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ReadCount++;
        if (PendingRead is not null) return PendingRead;
        return Task.FromResult(index switch
        {
            0x6041 => BitConverter.GetBytes(StatusReads.Count > 0 ? StatusReads.Dequeue() : StatusWord),
            0x6061 => [unchecked((byte)Mode)],
            0x606C => BitConverter.GetBytes(0),
            _ => new byte[expectedLength]
        });
    }
    public async Task WriteObjectAsync(ushort slave, ushort index, byte subIndex, byte[] data, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (PendingWrite is { } pending)
        {
            WriteEntered?.TrySetResult();
            await pending;
        }
        ct.ThrowIfCancellationRequested();
        Writes.Add((index, (byte[])data.Clone()));
        if (index == 0x6060 && !FreezeMode) Mode = unchecked((sbyte)data[0]);
        if (index == 0x6040 && !FreezeStatus)
        {
            var control = BitConverter.ToUInt16(data);
            var low = control & 0xF;
            StatusWord = low switch { 0 => 0x0040, 6 => 0x0021, 7 => 0x0023, 15 => 0x0027, 11 => 0x0007, _ => StatusWord };
            if (AutoCompletePosition && (control & 0x10) != 0) StatusWord |= 0x1400;
            if (AutoCompletePosition && (control & 0x10) == 0 && low == 15) StatusWord |= 0x0400;
        }
    }
    public Task<MotionSnapshot> ReadSnapshotAsync(CancellationToken ct) => Task.FromResult(Snapshot);
    public Task SubmitPdoAsync(string axisName, IReadOnlyList<MotionPdoWrite> writes, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        PdoWrites.Add(writes);
        return Task.CompletedTask;
    }
}
