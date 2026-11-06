namespace ZlgEtherCat.Motion;

/// <summary>运动专用通道访问。实现负责原生在途调用隔离、范围归属及唯一通道循环。</summary>
public interface IZlgMotionIo
{
    /// <summary>同步验证映像与真实从站范围并预留独占轴；不得执行设备写入。</summary>
    void ClaimAxis(MotionAxisConfiguration configuration);
    /// <summary>仅释放本轴归属；不得关闭父连接或提前释放在途原生资源。</summary>
    void ReleaseAxis(string axisName);
    Task<byte[]> ReadObjectAsync(ushort slave, ushort index, byte subIndex, int expectedLength, int timeoutMs, CancellationToken ct);
    Task WriteObjectAsync(ushort slave, ushort index, byte subIndex, byte[] data, int timeoutMs, CancellationToken ct);
    /// <summary>返回唯一采集器的完整输入快照；不得另行消费输入队列。</summary>
    Task<MotionSnapshot> ReadSnapshotAsync(CancellationToken ct);
    /// <summary>在轴所有权下原子合并输出；完成仅表示提交，不证明周期送达或物理执行。</summary>
    Task SubmitPdoAsync(string axisName, IReadOnlyList<MotionPdoWrite> writes, int timeoutMs, CancellationToken ct);
}

public sealed class MotionSnapshot
{
    private readonly byte[] _inputImage;
    public MotionSnapshot(byte[] inputImage, long sequence, long receivedTimestamp, bool qualityValid)
    {
        ArgumentNullException.ThrowIfNull(inputImage);
        _inputImage = (byte[])inputImage.Clone();
        Sequence = sequence;
        ReceivedTimestamp = receivedTimestamp;
        QualityValid = qualityValid;
    }
    public ReadOnlyMemory<byte> InputImage => _inputImage;
    public long Sequence { get; }
    /// <summary>主机读取完成时的 Stopwatch 单调时钟；不表示设备采样时间或此前 RX 排队时间。</summary>
    public long ReceivedTimestamp { get; }
    public string TimestampSource => "HostMonotonicReadCompletion";
    public long TimestampFrequencyHz => System.Diagnostics.Stopwatch.Frequency;
    public bool HardwareTimestampAvailable => false;
    public long? HardwareTimestamp => null;
    /// <summary>本地有效性，不能证明设备采样新鲜；已知积压或采集错误须为 false。</summary>
    public bool QualityValid { get; }
}

public sealed record MotionPdoWrite(int ByteOffset, byte[] Data);
/// <summary>PDO 使用对应快照的主机读取完成时刻；SDO 使用多次读取完成后的主机时刻，数据不保证原子一致。</summary>
public sealed record MotionFeedback(ushort StatusWord, sbyte? Mode, int? Position, int? Velocity, short? Torque, long Sequence, long ReceivedTimestamp)
{
    public string TimestampSource => "HostMonotonicReadCompletion";
    public long TimestampFrequencyHz => System.Diagnostics.Stopwatch.Frequency;
    public bool HardwareTimestampAvailable => false;
    public long? HardwareTimestamp => null;
}
public sealed record MotionDiagnostics(ushort StatusWord, MotionDriveState State, sbyte Mode, ushort ErrorCode, byte ErrorRegister);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum MotionMode : sbyte { PP = 1, PV = 3, PT = 4, HM = 6, CSP = 8, CSV = 9, CST = 10 }

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum MotionDriveState { Unknown, NotReady, SwitchOnDisabled, ReadyToSwitchOn, SwitchedOn, OperationEnabled, QuickStopActive, FaultReactionActive, Fault }

public sealed record MotionAxisConfiguration
{
    public string AxisName { get; init; } = "Axis1";
    public ushort SlaveIndex { get; init; }
    public MotionMode[] AllowedModes { get; init; } = [MotionMode.PP];
    public int MaxFeedbackAgeMs { get; init; } = 100;
    public int PollIntervalMs { get; init; } = 10;
    public int InputByteOffset { get; init; }
    public int InputByteLength { get; init; }
    public int OutputByteOffset { get; init; }
    public int OutputByteLength { get; init; }
    public int StatusWordOffset { get; init; } = -1;
    public int ActualPositionOffset { get; init; } = -1;
    public int ActualVelocityOffset { get; init; } = -1;
    public int ActualTorqueOffset { get; init; } = -1;
    public int ModeDisplayOffset { get; init; } = -1;
    public int ControlWordOffset { get; init; } = -1;
    public int TargetPositionOffset { get; init; } = -1;
    public int TargetVelocityOffset { get; init; } = -1;
    public int TargetTorqueOffset { get; init; } = -1;
    public int ModeCommandOffset { get; init; } = -1;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AxisName)) throw new ArgumentException("轴名称不能为空。");
        if (AllowedModes is null || AllowedModes.Length == 0 || AllowedModes.Any(m => !Enum.IsDefined(m)) || AllowedModes.Distinct().Count() != AllowedModes.Length)
            throw new ArgumentException("允许模式须为不重复的 1、3、4、6、8、9、10。");
        if (MaxFeedbackAgeMs <= 0 || PollIntervalMs is < 1 or > 1000) throw new ArgumentException("反馈有效期必须大于零，轮询间隔应为 1～1000 ms。");
        ValidateFields(InputByteOffset, InputByteLength,
            [(StatusWordOffset, 2), (ActualPositionOffset, 4), (ActualVelocityOffset, 4), (ActualTorqueOffset, 2), (ModeDisplayOffset, 1)]);
        ValidateFields(OutputByteOffset, OutputByteLength,
            [(ControlWordOffset, 2), (TargetPositionOffset, 4), (TargetVelocityOffset, 4), (TargetTorqueOffset, 2), (ModeCommandOffset, 1)]);
    }

    /// <summary>连续队列输出期间禁止邮箱访问，因此模式确认和停止反馈也必须在 PDO 中。</summary>
    public void ValidateQueueOutput()
    {
        Validate();
        if (StatusWordOffset < 0 || ControlWordOffset < 0 || ModeDisplayOffset < 0 || ActualVelocityOffset < 0)
            throw new InvalidOperationException($"轴 {AxisName} 启动连续队列输出前须映射状态字、控制字、模式显示和实际速度；这些字段用于模式确认、停止及去使能，不能在续发期间回退到 SDO。");
        foreach (var mode in AllowedModes)
        {
            var mapped = mode switch
            {
                MotionMode.CSP => TargetPositionOffset >= 0 && ActualPositionOffset >= 0,
                MotionMode.CSV => TargetVelocityOffset >= 0,
                MotionMode.CST => TargetTorqueOffset >= 0 && ActualTorqueOffset >= 0,
                _ => true
            };
            if (!mapped) throw new InvalidOperationException($"轴 {AxisName} 的 {mode} 目标及反馈 PDO 映射不完整，不能启动连续队列输出。");
        }
    }

    private static void ValidateFields(int start, int length, (int Offset, int Size)[] fields)
    {
        if (start < 0 || length < 0 || (long)start + length > int.MaxValue) throw new ArgumentException("轴过程映像范围无效。");
        var used = new List<(int Offset, int Size)>();
        foreach (var field in fields)
        {
            if (field.Offset == -1) continue;
            if (field.Offset < start || (long)field.Offset + field.Size > (long)start + length)
                throw new ArgumentException("轴 PDO 字段超出声明的映像范围，未映射字段必须为 -1。");
            if (used.Any(other => field.Offset < other.Offset + other.Size && other.Offset < field.Offset + field.Size))
                throw new ArgumentException("轴 PDO 字段存在重叠。");
            used.Add(field);
        }
    }
}
