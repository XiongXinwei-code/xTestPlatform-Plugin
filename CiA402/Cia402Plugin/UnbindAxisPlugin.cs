using CiA402.Executors;
using CiA402.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402;

public sealed class UnbindAxisPlugin : StepPluginBase<UnbindAxisSetting>
{
    public override string StepTypeId => "Motion.CiA402UnbindAxis";
    public override string DisplayName => "CiA402_Session_UnbindAxis";
    public override string Category => "Motion";
    public override string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_unbindaxis.png";

    public override string Description => """
        ## 功能
        
        解除 CiA402 轴绑定。
        
        ## 参数
        
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | AxisName | string([ExpressionField]) | 是 | "Axis1" | 求值结果为 string；必须先绑定该轴 |
        | TimeoutMs | int | 否 | 5000 | 步骤等待总超时；轴清理还受绑定时配置的 ShutdownTimeoutMs（默认 2000 ms）约束；等待结束不表示底层清理已退出 |
        | PollIntervalMs | int | 否 | 20 | 非实时轮询间隔，不代表完整周期 |
        
        ## 行为
        
        - 查找 AxisName 对应会话，尝试停止运动并释放使能，然后移除会话；不关闭共享总线。
        - 会话不存在时不报错，返回 Completed。清理失败或超时返回 Error，用户取消返回 Aborted，均移除终止会话。
        - 清理期限覆盖等待活动操作、停止运动、释放使能和底层释放；不随单次通信超时累计。达到期限后不再提交新的清理命令。
        - 未结束的底层调用继续保留节点占用，实际退出后仅释放一次；超时、取消或驱动释放失败不能视为物理停轴已确认。
        
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

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CiA402", () => new UnbindAxisExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var value = DeserializeSetting(setting);
        return $"解除 CiA402 轴绑定，轴：{value.AxisName}";
    }
}
