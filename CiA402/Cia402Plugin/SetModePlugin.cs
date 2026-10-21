using CiA402.Executors;
using CiA402.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402;

public sealed class SetModePlugin : StepPluginBase<SetModeSetting>
{
    public override string StepTypeId => "Motion.CiA402SetMode";
    public override string DisplayName => "CiA402_SetMode";
    public override string Category => "Motion";
    public override string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_setmode.png";

    public override string Description => """
        ## 功能
        
        切换驱动运行模式。
        
        ## 参数
        
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | Mode | OperationMode | 否 | OperationMode.PP | 写入 0x6060 并读取 0x6061 确认；不自动使能 |
        | AllowWhileEnabled | bool | 否 | false | 默认关闭；仅在已核实驱动支持、运动已停止且序列已停止周期目标更新时开启，不自动停轴 |
        | AxisName | string([ExpressionField]) | 是 | "Axis1" | 求值结果为 string；必须先绑定该轴 |
        | TimeoutMs | int | 否 | 5000 | 包括通信和轮询的总超时 |
        | PollIntervalMs | int | 否 | 20 | 非实时轮询间隔，不代表完整周期 |
        
        ## 行为
        
        - 支持 PP/PV/PT/HM/CSP/CSV/CST；AllowWhileEnabled 默认关闭，保留未运行使能状态切换。写入 0x6060 并等待 0x6061 确认。
        - 显式开启后，允许在 OperationEnabled 状态切换；由使用者核实设备支持，要求实际速度为零、启动/复位/Halt 标记已撤销。PP 须到位，速度/转矩模式目标须为零，CSP 目标须等于实际位置。
        - 先停止序列周期目标更新并完成上一周期 PDO/SYNC；位置模式准备当前位置目标，速度/转矩模式准备零目标，HM 不启动回零。相同实际模式只检查静止及使能状态，不重新初始化。
        - 全程确认未退出 OperationEnabled；不自动停轴以满足前置条件，不自动释放后重新使能，不隐式发送 SYNC。周期目标首次发送仍由通信映射决定何时应用。
        - 不支持对象、状态不符或切换超时返回错误；失败仍按控制步骤的一般规则尝试停止运动并释放使能。
        
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

    public override IStepExecutor CreateExecutor() => new SetModeExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var value = DeserializeSetting(setting);
        return $"切换驱动运行模式，轴：{value.AxisName}";
    }
}
