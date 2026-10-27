using MessagePack;
using xTestPlatform.Core.Models.StepSettings;
using CAN.Runtime;

namespace CAN.CANopen.Cyclic;

public enum CyclicOperation { ServiceStart, CycleStart, CycleInputsUpdate, SnapshotRead, CycleStop, ServiceStop }

[MessagePackObject(true)]
public sealed class CyclicSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"CAN1\"";
    [ExpressionField] public string ServiceName { get; set; } = "\"Feedback1\"";
    public List<FeedbackRouteSetting> FeedbackRoutes { get; set; } = [];
    public List<TargetFrameSetting> Targets { get; set; } = [];
    /// <summary>序列求值为逗号分隔节点列表；只用于控制占用，不修改设备配置。</summary>
    [ExpressionField] public string NodeIds { get; set; } = "\"1\"";
    /// <summary>当前步骤的 SYNC 配置：ServiceStart 为背景，CycleStart 为指令；二者独立。</summary>
    [ExpressionField] public string SyncCobId { get; set; } = "0x80";
    [ExpressionField] public string SyncData { get; set; } = "\"\"";
    public int PeriodMs { get; set; } = 10;
    public int CycleTimeoutMs { get; set; } = 1000;
    public int MaxLatenessMs { get; set; } = 100;
    public int InputMaxAgeMs { get; set; } = 1000;
    public int StopTimeoutMs { get; set; } = 5000;
    public bool EnableMonitoringSync { get; set; }
    /// <summary>背景 SYNC 独立起始间隔；目标 RPDO 后的额外 SYNC 不重置本周期。</summary>
    public int MonitoringPeriodMs { get; set; } = 10;
    public bool ResynchronizeLateMonitoring { get; set; } = true;
    public int MonitoringConsecutiveLatenessLimit { get; set; } = 3;
    public bool SuperviseFeedback { get; set; }
    public int FeedbackStartupGraceMs { get; set; } = 1000;
    public bool UseFiniteTrajectory { get; set; }
    public int TrajectoryDurationMs { get; set; } = 1000;
    public TrajectoryMissPolicy MissPolicy { get; set; } = TrajectoryMissPolicy.SkipAndCatchUp;
    public int LatenessWindowMs { get; set; }
    public int MaxSkippedPoints { get; set; } = 20;
    public int HoldTimeoutMs { get; set; } = 100;
    [ExpressionField] public string TrajectoryJson { get; set; } = "\"[]\"";
    [ExpressionField] public string StopCommandsJson { get; set; } = "\"[]\"";
    /// <summary>默认仅提交；true为独占逐批确认诊断，不能与其它发送源混跑。</summary>
    public bool RequireOnBusConfirmation { get; set; }
    public bool RequireAllValid { get; set; }
    /// <summary>已声明 string 变量，输出完整 JSON 快照，不依赖 SDK 任意 DTO 赋值能力。</summary>
    [VariablePathField] public string ResultVariable { get; set; } = "";
}

[MessagePackObject(true)]
public sealed class FeedbackRouteSetting
{
    public string Name { get; set; } = "TPDO";
    [ExpressionField] public string CobId { get; set; } = "0x181";
    public int Dlc { get; set; } = 8;
    public int MaxAgeMs { get; set; } = 100;
}

[MessagePackObject(true)]
public sealed class TargetFrameSetting
{
    [ExpressionField] public string CobId { get; set; } = "0x201";
    [ExpressionField] public string Data { get; set; } = "\"00 00 00 00\"";
    public int Dlc { get; set; } = 4;
}
