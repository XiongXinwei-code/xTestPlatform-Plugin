using System.Buffers.Binary;
using System.Diagnostics;

namespace ZlgEtherCat.Motion;

public sealed partial class ZlgMotionAxis
{
    private async Task<MotionSnapshot> ReadSnapshotCoreAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var snapshot = await _io.ReadSnapshotAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var age = Stopwatch.GetElapsedTime(snapshot.ReceivedTimestamp).TotalMilliseconds;
        if (!snapshot.QualityValid || snapshot.Sequence <= 0 || age < 0 || age > _configuration.MaxFeedbackAgeMs)
            throw new InvalidDataException("轴 PDO 反馈无效、积压或已超过本地有效期限；本地主机时间不证明设备采样新鲜度。");
        if ((long)_configuration.InputByteOffset + _configuration.InputByteLength > snapshot.InputImage.Length)
            throw new InvalidDataException("轴输入映像范围超过当前完整采集快照长度。");
        return snapshot;
    }

    private async Task<ushort> ReadStatusCoreAsync(CancellationToken ct)
    {
        if (_configuration.StatusWordOffset < 0)
            return BinaryPrimitives.ReadUInt16LittleEndian(await ReadObjectAsync(0x6041, 0, 2, ct).ConfigureAwait(false));
        var snapshot = await ReadSnapshotCoreAsync(ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt16LittleEndian(snapshot.InputImage.Span[_configuration.StatusWordOffset..]);
    }

    private async Task<sbyte> ReadModeCoreAsync(CancellationToken ct)
    {
        if (_configuration.ModeDisplayOffset < 0)
            return unchecked((sbyte)(await ReadObjectAsync(0x6061, 0, 1, ct).ConfigureAwait(false))[0]);
        var snapshot = await ReadSnapshotCoreAsync(ct).ConfigureAwait(false);
        return unchecked((sbyte)snapshot.InputImage.Span[_configuration.ModeDisplayOffset]);
    }

    private async Task<MotionFeedback> ReadFeedbackCoreAsync(CancellationToken ct)
    {
        if (_configuration.InputByteLength == 0)
        {
            // 无 PDO 映射时的 SDO 反馈为顺序读取，不能作为同周期原子快照。
            var status = await ReadStatusCoreAsync(ct).ConfigureAwait(false);
            var mode = await ReadModeCoreAsync(ct).ConfigureAwait(false);
            var position = BinaryPrimitives.ReadInt32LittleEndian(await ReadObjectAsync(0x6064, 0, 4, ct).ConfigureAwait(false));
            var velocity = BinaryPrimitives.ReadInt32LittleEndian(await ReadObjectAsync(0x606C, 0, 4, ct).ConfigureAwait(false));
            var torque = BinaryPrimitives.ReadInt16LittleEndian(await ReadObjectAsync(0x6077, 0, 2, ct).ConfigureAwait(false));
            return new(status, mode, position, velocity, torque, 0, Stopwatch.GetTimestamp());
        }
        if (_configuration.StatusWordOffset < 0) throw new InvalidOperationException("PDO 轴反馈必须配置状态字偏移。");
        var snapshot = await ReadSnapshotCoreAsync(ct).ConfigureAwait(false);
        return new(
            BinaryPrimitives.ReadUInt16LittleEndian(snapshot.InputImage.Span[_configuration.StatusWordOffset..]),
            _configuration.ModeDisplayOffset < 0 ? null : unchecked((sbyte)snapshot.InputImage.Span[_configuration.ModeDisplayOffset]),
            _configuration.ActualPositionOffset < 0 ? null : BinaryPrimitives.ReadInt32LittleEndian(snapshot.InputImage.Span[_configuration.ActualPositionOffset..]),
            _configuration.ActualVelocityOffset < 0 ? null : BinaryPrimitives.ReadInt32LittleEndian(snapshot.InputImage.Span[_configuration.ActualVelocityOffset..]),
            _configuration.ActualTorqueOffset < 0 ? null : BinaryPrimitives.ReadInt16LittleEndian(snapshot.InputImage.Span[_configuration.ActualTorqueOffset..]),
            snapshot.Sequence, snapshot.ReceivedTimestamp);
    }

    private async Task<int> ReadPositionCoreAsync(CancellationToken ct)
    {
        if (_configuration.ActualPositionOffset < 0)
            return BinaryPrimitives.ReadInt32LittleEndian(await ReadObjectAsync(0x6064, 0, 4, ct).ConfigureAwait(false));
        var snapshot = await ReadSnapshotCoreAsync(ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(snapshot.InputImage.Span[_configuration.ActualPositionOffset..]);
    }

    private async Task<(int Velocity, long Sequence)> ReadVelocityCoreAsync(CancellationToken ct)
    {
        if (_configuration.ActualVelocityOffset < 0)
            return (BinaryPrimitives.ReadInt32LittleEndian(await ReadObjectAsync(0x606C, 0, 4, ct).ConfigureAwait(false)), 0);
        var snapshot = await ReadSnapshotCoreAsync(ct).ConfigureAwait(false);
        return (BinaryPrimitives.ReadInt32LittleEndian(snapshot.InputImage.Span[_configuration.ActualVelocityOffset..]), snapshot.Sequence);
    }
}
