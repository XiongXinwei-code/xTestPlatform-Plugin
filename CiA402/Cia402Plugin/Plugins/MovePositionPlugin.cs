using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("MovePosition", "执行 PP 位置运动", "- 要求实际 PP 模式及运行使能；处理新目标确认握手。\n- WaitForTarget=true 等待本次运动到位，否则目标握手完成后返回，电机可能仍在运行。")]
public sealed class MovePositionPlugin : Cia402Plugin<MovePositionSetting, MovePositionExecutor> { }
