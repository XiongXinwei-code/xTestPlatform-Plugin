using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("StopMotion", "停止驱动运动", "- 使用驱动 0x605A 指定的减速停止策略（支持选项 1~8），等待实际速度为零且退出 Operation Enabled。\n- 不自动重新使能；必须先按设备手册配置停止参数；不支持该策略时返回错误。")]
public sealed class StopMotionPlugin : Cia402Plugin<StopMotionSetting, StopMotionExecutor> { }
