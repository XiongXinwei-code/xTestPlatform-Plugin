using CiA402.Executors;
using CiA402.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402;

public sealed class ReadFeedbackPlugin : StepPluginBase<ReadFeedbackSetting>
{
    public override string StepTypeId => "Motion.CiA402ReadFeedback";
    public override string DisplayName => "CiA402_Feedback_ReadFeedback";
    public override string Category => "Motion";
    public override string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_readfeedback.png";

    public override string Description => """
        ## 功能
        
        读取轴反馈。
        
        ## 参数
        
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | StatusWordVariable | string([VariablePathField]) | 否 | — | 完整变量路径，double；留空不写 |
        | PositionVariable | string([VariablePathField]) | 否 | — | 完整变量路径，double，设备原始单位；TPDO 必须映射该值 |
        | VelocityVariable | string([VariablePathField]) | 否 | — | 完整变量路径，double，设备原始单位；TPDO 必须映射该值 |
        | TorqueVariable | string([VariablePathField]) | 否 | — | 完整变量路径，double，设备原始单位；TPDO 必须映射该值 |
        | AxisName | string([ExpressionField]) | 是 | "Axis1" | 求值结果为 string；必须先绑定该轴 |
        | TimeoutMs | int | 否 | 5000 | 包括通信和轮询的总超时 |
        | PollIntervalMs | int | 否 | 20 | 非实时轮询间隔，不代表完整周期 |
        
        ## 行为
        
        - 绑定时选择 SDO 顺序读取、单 TPDO 或多 TPDO；TPDO 整体未映射所需输出时返回错误。
        - 多 TPDO 只收集本次请求需要的来源，所有帧共用单次通信期限；逐 COB-ID 检查时间戳，队列年龄及帧间时间跨度须在 FeedbackMaxAgeMs 窗口内，不保证同一 SYNC 周期。
        - 被动 TPDO 接收超时仍返回 Error 且不更新输出变量；原接收实际退出后可退避重读，不因取消调度窗口永久隔离整轴。调用未退出前后续 I/O 仍排队，不允许重入或绕过共享 CAN 失效保护。
        - SDO 反馈包含协议请求，不属于被动接收；事务中断后的协议隔离仍保留。不会将旧反馈或默认零值作为本次有效结果。
        - SDO 多对象读取不是原子快照；TPDO 检测重复硬件时间戳和接收队列驻留时间，但不能证明硬件采样年龄。
        - 没有有效反馈时超时报错，不用旧值冒充本次数据。
        
        - 正常执行返回 Completed；参数、通信、设备状态问题及超时返回 Error；用户取消返回 Aborted。
        - 仅写入用户已声明并明确配置的完整变量路径，空输出路径不写入。
        - 周期目标由测试序列循环编排，属于非实时周期控制，不保证确定性周期；不启动后台周期任务或隐式发送 SYNC。
        - 后端声明支持时，运动等待期间可由并行序列调用 CiA402 读取步骤独立请求硬件反馈；同轴仅一个控制流程，底层传输逐次协调。排队和通信受超时约束，不以缓存值替代失败读取。
        - CANopen 轴占用期间不要混用同节点原始 SDO/PDO、CAN 接收或后台周期写入。关闭 CAN 前先解除轴绑定。
        
        ## 相关插件
        
        - CiA402_BindAxis、CiA402_SetMode、CiA402_Enable
        - CiA402_ReadState、CiA402_StopMotion、CiA402_Disable、CiA402_UnbindAxis
        - CAN_Open、CANopen_SyncSend
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CiA402", () => new ReadFeedbackExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var value = DeserializeSetting(setting);
        return $"读取轴反馈，轴：{value.AxisName}";
    }
}
