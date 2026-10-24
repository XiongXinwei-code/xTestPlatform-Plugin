using CiA402.Executors;
using CiA402.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402;

public sealed class RunVelocityPlugin : StepPluginBase<RunVelocitySetting>
{
    public override string StepTypeId => "Motion.CiA402RunVelocity";
    public override string DisplayName => "CiA402_Motion_RunVelocity";
    public override string Category => "Motion";
    public override string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_runvelocity.png";

    public override string Description => """
        ## 功能
        
        设置 PV 目标速度。
        
        ## 参数
        
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | TargetVelocity | string([ExpressionField]) | 是 | 0 | 求值结果为整数，设备原始速度单位 |
        | Acceleration | string([ExpressionField]) | 是 | 100 | 求值结果为整数，设备原始单位 |
        | Deceleration | string([ExpressionField]) | 是 | 100 | 求值结果为整数，设备原始单位 |
        | AxisName | string([ExpressionField]) | 是 | "Axis1" | 求值结果为 string；必须先绑定该轴 |
        | TimeoutMs | int | 否 | 5000 | 包括通信和轮询的总超时 |
        | PollIntervalMs | int | 否 | 20 | 非实时轮询间隔，不代表完整周期 |
        
        ## 行为
        
        - 要求实际 PV 模式及运行使能；下发轮廓参数和目标速度。
        - Completed 表示写入完成，不表示已达到目标速度或已经停止。
        
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

    public override IStepExecutor CreateExecutor() => new RunVelocityExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var value = DeserializeSetting(setting);
        return $"设置 PV 目标速度，轴：{value.AxisName}";
    }
}
