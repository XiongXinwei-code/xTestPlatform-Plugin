using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("RunTorque", "设置 PT 目标转矩", "- 要求实际 PT 模式及运行使能；下发转矩斜率和目标。\n- Completed 表示写入完成，不表示转矩已达到或电机已停止；单位以驱动配置为准。")]
public sealed class RunTorquePlugin : Cia402Plugin<RunTorqueSetting, RunTorqueExecutor> { }
