using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("RunVelocity", "设置 PV 目标速度", "- 要求实际 PV 模式及运行使能；下发轮廓参数和目标速度。\n- Completed 表示写入完成，不表示已达到目标速度或已经停止。")]
public sealed class RunVelocityPlugin : Cia402Plugin<RunVelocitySetting, RunVelocityExecutor> { }
