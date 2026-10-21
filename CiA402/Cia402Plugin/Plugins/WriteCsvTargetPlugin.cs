using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("WriteCsvTarget", "写入单次 CSV 速度目标", "- 要求实际 CSV 模式；允许在未运行使能状态提交初始目标，首个目标必须为零。\n- 使用已验证 RPDO 发送一次，不执行内部循环。")]
public sealed class WriteCsvTargetPlugin : Cia402Plugin<WriteCsvTargetSetting, WriteCsvTargetExecutor> { }
