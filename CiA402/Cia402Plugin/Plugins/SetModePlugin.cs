using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("SetMode", "切换驱动运行模式", "- 支持 PP/PV/PT/HM/CSP/CSV/CST，驱动必须处于未运行使能状态；写入 0x6060 并等待 0x6061 确认。\n- 不支持对象或模式时返回错误，不隐式启动运动。")]
public sealed class SetModePlugin : Cia402Plugin<SetModeSetting, SetModeExecutor> { }
