using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("SetMode", "切换驱动运行模式", "- 支持 PP/PV/PT/HM/CSP/CSV/CST；AllowWhileEnabled 默认关闭，保留未运行使能状态切换。写入 0x6060 并等待 0x6061 确认。\n- 显式开启后，允许在 OperationEnabled 状态切换；由使用者核实设备支持，要求实际速度为零、启动/复位/Halt 标记已撤销。PP 须到位，速度/转矩模式目标须为零，CSP 目标须等于实际位置。\n- 先停止序列周期目标更新并完成上一周期 PDO/SYNC；位置模式准备当前位置目标，速度/转矩模式准备零目标，HM 不启动回零。相同实际模式只检查静止及使能状态，不重新初始化。\n- 全程确认未退出 OperationEnabled；不自动停轴以满足前置条件，不自动释放后重新使能，不隐式发送 SYNC。周期目标首次发送仍由通信映射决定何时应用。\n- 不支持对象、状态不符或切换超时返回错误；失败仍按控制步骤的一般规则尝试停止运动并释放使能。")]
public sealed class SetModePlugin : Cia402Plugin<SetModeSetting, SetModeExecutor> { }
