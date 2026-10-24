using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.CANopen.Cyclic;

public abstract class CyclicPluginBase : StepPluginBase<CyclicSetting>
{
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
        CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。
        ## 参数
        ConnectionName、ServiceName 为字符串表达式；FeedbackRoutes 配置名称、COB-ID 表达式、DLC、MaxAgeMs。
        Targets 按列表顺序配置 COB-ID / Data 表达式及 DLC；Data 求值为十六进制字符串或 byte[]，不改变字节顺序。
        NodeIds 求值为逗号分隔节点列表；SYNC 的 ID 和数据显式配置。
        PeriodMs、CycleTimeoutMs、MaxLatenessMs、InputMaxAgeMs、StopTimeoutMs 单位毫秒。
        StopTimeoutMs 默认 5000；ServiceStart 启用监测 SYNC 时用作启动失败的收尾期限，停止步骤用作退出确认期限；等待到期仍保留未实际退出的占用。
        ResultVariable 为已声明的 string 变量路径，写入完整 JSON 快照；RequireAllValid 可要求所有反馈可信有效。
        RequireOnBusConfirmation 默认 true；适配器不能证明发送完成时拒绝启动。
        ## 行为
        - ServiceStart 默认仅接收；显式 EnableMonitoringSync 后启动连接唯一 SYNC 调度者，MonitoringPeriodMs 独立配置。
        - CycleStart 保留 NodeIds 控制占用；与 CiA402 占用冲突时报错，重复启动须先确认 CycleStop。
        - CycleInputsUpdate 在当前序列步骤求值整组目标并原子发布；后台不读取活动序列上下文。
        - SnapshotRead 不等待报文，输出接收质量、时间域、队列统计和后台错误；未知时间不伪装有效。
        - CycleStop 冻结新目标并处理发送收尾；配置的 StopCommandsJson 通过当前节点所有者串行执行 SDO。监测模式要求已配置停机命令。
        - 停机 SDO 全部应答且无部分周期/未知待发时回到监测；停止命令应答不是电机停稳证明。
        - UseFiniteTrajectory 时 TrajectoryJson（字符串表达式）提供 OffsetMs/Frames/CobId/DataHex 点表，TrajectoryDurationMs 为总时长，不要求 InputMaxAgeMs 续期。
        - 点表按绝对偏移逐点执行，错过下一点时报错，不密集补发或静默跳点。SuperviseFeedback 可监督每路反馈有效期。
        - ServiceStop 确认发送退出后移除本服务；名称不存在时报错，不关闭 CAN 连接。
        - 关闭期限届满保留隔离与占用。发送退出不等价于电机停止；Windows 软件周期不提供硬实时保证。
        - 取消严格确认仅用于明确接受主机提交顺序的验证；旧帧待发状态未知时 Stop 仍报错并保留占用。
        - 受控模式禁止 UDS/XCP/raw CAN_Read 混用；模式持续至 CAN_Close。
        """;
    public override IStepExecutor CreateExecutor() => new CyclicExecutor(this);
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
