using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using MessagePack;

namespace CAN.CANopen.Cyclic;

public abstract class CyclicPluginBase : StepPluginBase<CyclicSetting>
{
    protected override int CurrentSettingVersion => 2;
    protected override CyclicSetting MigrateSetting(byte[] data, int fromVersion)
    {
        if (fromVersion != 1) return base.MigrateSetting(data, fromVersion);
        var setting = MessagePackSerializer.Deserialize<CyclicSetting>(data, SerializerOptions);
        setting.RequireOnBusConfirmation = false; // User-authorized v1 cycle semantic migration.
        return setting;
    }
    public abstract CyclicOperation Operation { get; }
    public override string StepTypeId => $"CANopen.{Operation}";
    public override string DisplayName => Operation switch
    {
        CyclicOperation.ServiceStart => "CANopen_Service_Start",
        CyclicOperation.CycleStart => "CANopen_Cycle_Start",
        CyclicOperation.CycleInputsUpdate => "CANopen_Cycle_InputsUpdate",
        CyclicOperation.SnapshotRead => "CANopen_Service_SnapshotRead",
        CyclicOperation.CycleStop => "CANopen_Cycle_Stop",
        CyclicOperation.ServiceStop => "CANopen_Service_Stop",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation))
    };
    public override string Category => "Communication";
    public override string IconPath => CanopenPluginInfo.IconBase + "canopen_pdo_receive.png";
    public override string Description => """
        ## 功能
        CAN / CANopen持续接收、独立最新快照及多个具名周期发送源。
        ## 参数
        ConnectionName/ServiceName为字符串表达式。FeedbackRoutes为0~128路名称/COB-ID表达式/DLC/MaxAgeMs配置；非纯SYNC服务至少一路，反馈不固定四路。
        Targets按列表顺序配置COB-ID/Data表达式/DLC；Data为十六进制字符串或byte[]，保持原始字节。NodeIds为逗号分隔节点字符串表达式。SyncCobId/SyncData是当前源独立配置，SYNC允许DLC0或1。
        PeriodMs控制RPDO＋额外SYNC周期；MonitoringPeriodMs控制背景SYNC。CycleTimeoutMs约束单批提交/排队总期限，与周期分开；MaxLatenessMs约束起始迟到，InputMaxAgeMs约束实时目标断供。StopTimeoutMs为停止/关闭及启动失败收尾总期限，默认5000ms，尊重用户配置。
        RequireOnBusConfirmation默认false：PDO/SYNC只提交、不等待回显或会话完成。true仅作独占诊断，不能与其它发送源混跑，同源背景/目标模式须一致。设置版本1显式迁移为false；版本2可保留显式true。
        ResynchronizeLateMonitoring默认true，纯监测迟到跳拍重同步、不补发；MonitoringConsecutiveLatenessLimit默认3（1~10000）。准时起始、进入/退出运动清连续计数；false保留严格行为。
        UseFiniteTrajectory启用不可变TrajectoryJson点表（OffsetMs/Frames/CobId/DataHex），TrajectoryDurationMs为总时长，不要求实时续期。MissPolicy默认SkipAndCatchUp只发最新到期点；Fail保留严格错点失败；HoldLast有限重复上次已提交字节，必须配置停机SDO。
        LatenessWindowMs默认0表示PeriodMs；MaxSkippedPoints默认20为累计跳点上限；HoldTimeoutMs默认100为保持上限，不延长总时长。裸字节跳点不保证电机速度/加速度连续性，由序列规划层审查。
        SuperviseFeedback监督每路有效期，FeedbackStartupGraceMs为宽限；ResultVariable写已声明string路径的完整JSON，RequireAllValid可要求全部反馈可信有效。
        StopCommandsJson提供经设备审查的NodeId/Index/SubIndex/DataHex停机SDO列表；插件不猜测控制字，ACK不证明物理停稳。
        ## 行为
        - ServiceStart默认仅接收；EnableMonitoringSync显式启动本服务SYNC。不同ServiceName可创建多个独立源、共享SYNC ID，各自配置和停止，不限制两个。
        - 连接FIFO队列项为完整[RPDO1,...,SYNC]或[SYNC]批次。同一单调时刻入队时按批次首帧ID决胜；已入队不拆散、不按成员ID排序、不插入其它源。队列有界，满时明确拒绝。
        - 背景周期从自身批次起始计时；目标的额外SYNC不重置或合并背景SYNC。后台只用复制数据，不读取活动序列表达式。Windows软件周期不提供硬实时保证。
        - NI正常周期一次AsSubmitted整批写入，SDK成功返回即结束提交，不轮询session、不等echo。SubmissionCompletedFrames与ConfirmedFrames分账，SDK接受不代表已上总线或电机停稳。其它适配器在同一整批租约内串行Write；实际SDK线上排队与外部发送者需硬件验收，Strategy注明路径。
        - SDO仍等待协议响应；允许同连接CAN/CANopen/CiA402补充读写，完整事务串行。原始CAN观察、CANopen协议和每轴反馈使用独立副本，共享唯一硬件接收泵，外部模式/映射/目标改动由序列协调。
        - CycleInputsUpdate原子发布同数量/布局整组字节。有限点表首次时钟锚在首批FIFO准入，后续每次准入重新选点和检查有效期；初点不可跳过、终点须真实提交，期限外不强发终点，不密集补发。
        - CycleStop冻结目标，撤销未准入整批；已准入批次完整提交后按可选停机SDO收尾。只验证本次运动代次的主机退出，保留背景/其它源/接收；不等于电机停稳。
        - PartialCycle、SDK失败、未退原生调用不能假成功；软等待结束不等于撤回报文。正常仅提交结束不要求echo，不补写confirmed或停机ACK。
        - ServiceStop释放本服务订阅/节点/发送占用。本次清理已完成可返回Completed且保留历史故障；ServiceCleanupCompleted/HostSubmissionQuiescent是主机退出，ServiceShutdownConfirmed仍要求真实确认。
        - CAN_Close独立冻结所有源，等待真实I/O/回调退出并检查SDK关闭，不要求此前ServiceStop或SDO停机成功；未退出/关闭失败保留句柄，合法关闭后允许下一次打开。
        - SnapshotRead不等报文；Receive.Running只表示接收侧，ServiceHealthy/BackgroundError汇总健康。每路独立判有效期，未知时间保持TimeUnknown，不以出队时刻更新帧龄。
        - Timing.Batch记录主机锁/SDK提交/原生退出与接受范围；AdapterBatchMs不是纯总线时间。SkippedTrajectoryPoints、HeldTrajectoryCycles、TerminalSubmitted不代表物理到位。PendingFrames/FifoEvictions、LatestReplacements、QueueStatistics分别属于共享每ID FIFO、快照和适配器，不能混算丢帧。
        - 周期源间RPDO/历史ID冲突仍拒绝；普通CAN/PDO/SYNC补充发送可排队使用同ID，不插入完整批次。通用周期任务可共存；显式确认诊断只排斥其它后台发送源。UDS/XCP协议混用保持原限制。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CyclicExecutor(this));
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"{Operation} {s.ConnectionName}/{s.ServiceName}";
    }
}

public sealed class ServiceStartPlugin : CyclicPluginBase { public override CyclicOperation Operation => CyclicOperation.ServiceStart; }
public sealed class CycleStartPlugin : CyclicPluginBase { public override CyclicOperation Operation => CyclicOperation.CycleStart; }
public sealed class CycleInputsUpdatePlugin : CyclicPluginBase { public override CyclicOperation Operation => CyclicOperation.CycleInputsUpdate; }
public sealed class SnapshotReadPlugin : CyclicPluginBase { public override CyclicOperation Operation => CyclicOperation.SnapshotRead; }
public sealed class CycleStopPlugin : CyclicPluginBase { public override CyclicOperation Operation => CyclicOperation.CycleStop; }
public sealed class ServiceStopPlugin : CyclicPluginBase { public override CyclicOperation Operation => CyclicOperation.ServiceStop; }
