using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;

namespace CiA402.Tests;

public sealed class FakeAxisTransport : ICia402AxisTransport
{
    public bool SupportsConcurrentMonitoring { get; set; }
    public ushort Status { get; set; } = 0x40;
    public ushort Control { get; set; }
    public OperationMode Mode { get; set; } = OperationMode.PP;
    public int Position { get; set; } = 100;
    public int Velocity { get; set; }
    public bool Freeze { get; set; }
    public bool RejectMode { get; set; }
    public bool DropEnableOnModeChange { get; set; }
    public bool CompletePosition { get; set; } = true;
    public TaskCompletionSource PositionHandshakeCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool HomingError { get; set; }
    public bool Disposed { get; private set; }
    public Dictionary<(ushort, byte), byte[]> Objects { get; } = new()
    {
        [(0x607A, 0)] = ObjectCodec.Encode(100, 4, true),
        [(0x60FF, 0)] = ObjectCodec.Encode(0, 4, true),
        [(0x6071, 0)] = ObjectCodec.Encode(0, 2, true)
    };
    public List<(ushort Index, byte Sub, byte[] Data)> Writes { get; } = new();
    public List<(OperationMode Mode, long Target, ushort Control)> Targets { get; } = new();

    public Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes = index switch
        {
            0x6040 => ObjectCodec.Encode(Control, 2, false), 0x6041 => ObjectCodec.Encode(Status, 2, false),
            0x6061 => ObjectCodec.Encode((sbyte)Mode, 1, true), 0x6064 => ObjectCodec.Encode(Position, 4, true),
            0x606C => ObjectCodec.Encode(Velocity, 4, true), 0x605A => ObjectCodec.Encode(2, 2, true),
            0x603F => ObjectCodec.Encode(0, 2, false),
            _ => Objects.TryGetValue((index, subIndex), out var value) ? value : throw new InvalidOperationException($"仿真对象不存在 0x{index:X4}:{subIndex}")
        };
        return Task.FromResult(bytes.ToArray());
    }

    public Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Writes.Add((index, subIndex, value.ToArray()));
        Objects[(index, subIndex)] = value.ToArray();
        if (index == 0x6060 && !RejectMode)
        {
            Mode = (OperationMode)(sbyte)value[0];
            if (DropEnableOnModeChange) Status = 0x23;
        }
        if (index == 0x6040)
        {
            var previous = Control;
            Control = (ushort)ObjectCodec.Decode(value, 2, false);
            if (!Freeze)
            {
                if ((Control & 0x80) != 0 && (previous & 0x80) == 0 && AxisController.DecodeState(Status) == DriveState.Fault) Status = 0x40;
                else if (AxisController.DecodeState(Status) != DriveState.Fault)
                {
                    Status = (Control & 0xF) switch { 6 => 0x21, 7 => 0x23, 15 => 0x27, 2 => 0x07, 0 => 0x40, _ => Status };
                    if ((Control & 0xF) == 2) Velocity = 0;
                    if (Mode == OperationMode.PP)
                    {
                        if ((Control & 0x10) != 0)
                        {
                            Status |= 0x1000;
                            if (CompletePosition && Objects.TryGetValue((0x607A, 0), out var p)) Position = (int)ObjectCodec.Decode(p, 4, true);
                        }
                        if (CompletePosition) Status |= 0x0400;
                        if ((Control & 0x10) == 0 && (previous & 0x10) != 0) PositionHandshakeCompleted.TrySetResult();
                    }
                    if (Mode == OperationMode.HM && (Control & 0x10) != 0) Status |= (ushort)(HomingError ? 0x2000 : 0x1400);
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task WriteTargetAsync(OperationMode mode, long target, ushort controlWord, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Targets.Add((mode, target, controlWord));
        return Task.CompletedTask;
    }
    public Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct)
        => Task.FromResult(new AxisFeedback(Status, position ? Position : null, velocity ? Velocity : null, torque ? (short)0 : null));
    public void RequireCyclicMode(OperationMode mode) { if (mode is not (OperationMode.CSP or OperationMode.CSV or OperationMode.CST)) throw new ArgumentException("不是周期模式"); }
    public void Dispose() => Disposed = true;
}
